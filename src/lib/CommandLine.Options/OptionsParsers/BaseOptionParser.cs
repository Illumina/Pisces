using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CommandLine.NDesk.Options;
using CommandLine.Util;
using Common.IO.Utility;

namespace CommandLine.Options
{
    public interface IOptionParser
    {
        Dictionary<string, OptionSet> GetParsingMethods();
        CommandLineParseResult ParsingResult { get; }
         Dictionary<string, OptionSet> OptionSetDics { get; }
        IApplicationOptions Options { get; set; }
        void ParseArgs(string[] args, bool includeValidation = true);

    }

    public abstract class BaseOptionParser : IOptionParser
    {
        public static List<T> ParseCommaSeparatedFieldToList<T>(string field) where T : struct, IConvertible
        {

            if (field != null)
            {
                var strings = field.Split(",", StringSplitOptions.RemoveEmptyEntries);
                var values = new List<T>();

                foreach (var s in strings)
                {
                    var success = Enum.TryParse(s, out T classification);
                    if (success)
                    {
                        values.Add(classification);
                    }
                    else
                    {
                        throw new ArgumentException($"Unrecognized value '{classification}' for type {typeof(T)}");
                    }
                }

                if (string.Join(",", values) != field)
                {
                    throw new ArgumentException($"Issue parsing values: '{field}'");
                }

                return values;
            }
            return new List<T>();
        }

        public static List<string> ParseCommaSeparatedFieldToList(string field) 
        {

            if (field != null)
            {
                var strings = field.Split(",", StringSplitOptions.RemoveEmptyEntries);
                return strings.ToList();
            }
            return new List<string>();
        }

        private CommandLineParseResult _ParsingResult = new CommandLineParseResult();
        private Dictionary<string, OptionSet> _OptionSetDics;
        private IApplicationOptions _Options;

        public string[] CommandLineArguments;

        public abstract void ValidateOptions();


        public abstract Dictionary<string, OptionSet> GetParsingMethods();


        public void ParseArgs(string[] args, bool includeValidation = true)
        {
            CommandLineArguments = args;
            var dictionary = GetParsingMethods();
            AddBasicParsing(dictionary, ParsingResult);
            DoGenericParsingAndValidation(args, dictionary, includeValidation);
            Options.CommandLineArguments = CommandLineArguments;
        }

        public IApplicationOptions Options { get => _Options; set => _Options = value; }
        public CommandLineParseResult ParsingResult { get => _ParsingResult; }
        public bool HadSuccess { get => ParsingResult.ExitCode==  (int) ExitCodeType.Success; }
        public bool ParsingFailed { get => !HadSuccess; }
        public Dictionary<string, OptionSet> OptionSetDics { get => _OptionSetDics; set => _OptionSetDics = value; }

        public static Dictionary<string, OptionSet> GetCommonParsingMethods(CommandLineParseResult parseResult)
        {

            var commonOptions = new OptionSet
            {
                {
                    "help|h", "displays the help menu", v => parseResult.ShowHelpMenu = v != null
                },
                {
                    "version|v", "displays the version", v => parseResult.ShowVersion = v != null
                }
            };

            var optionDict = new Dictionary<string, OptionSet>
            {
                { OptionSetNames.Common,commonOptions},
            };

            return optionDict;
        }

        private static void AddBasicParsing(Dictionary<string, OptionSet> parsingMethods, CommandLineParseResult parseResult)
        {
            var commonParsingMethods = GetCommonParsingMethods(parseResult);


            foreach (var key in commonParsingMethods.Keys)
            {
                foreach (var optSet in commonParsingMethods[key])
                {
                    if (!parsingMethods.ContainsKey(key))
                    {
                        parsingMethods.Add(key, new OptionSet());
                    }

                    parsingMethods[key].Add(optSet);
                }
            }
        }

        public void DoGenericParsingAndValidation(string[] args, Dictionary<string, OptionSet> optionSetDics, bool includeValidationWithParsing = true)
        {
            
            OptionSetDics = optionSetDics;
            ParsingResult.ErrorSpacer = new string(' ', 7);
            ParsingResult.ErrorBuilder = new StringBuilder();

            if (args == null || args.Length == 0)
            {
                ParsingResult.UpdateExitCode(ExitCodeType.MissingCommandLineOption);
                ParsingResult.ShowHelpMenu = true;
            }

            //unsupported options are going to be not found in every optionSetDictionary (ie, Required, Common, And DefaultOptions )
            List<string> optionsNotSupportedByAnyDictionary = null;

            try
            {
                //do parsing, and collect wrong arguments

                var optionsUsed = new Dictionary<string, string>();
                foreach (var ops in optionSetDics)
                {
                    var currentUnsupportedOps = ops.Value.Parse(args);
                    var dict = ops.Value.GetOptionsUsed();
                    foreach (var kvp in dict)
                    {
                        optionsUsed.Add(kvp.Key, kvp.Value);
                    }

                    //sanitize
                    currentUnsupportedOps.RemoveAll((c) => c == "");
                    currentUnsupportedOps.RemoveAll((c) => c[0] != '-');

                    if (optionsNotSupportedByAnyDictionary != null)
                        optionsNotSupportedByAnyDictionary = optionsNotSupportedByAnyDictionary.Intersect(currentUnsupportedOps).ToList();

                    else
                        optionsNotSupportedByAnyDictionary = currentUnsupportedOps;

                }

                ParsingResult.OptionsUsed = optionsUsed;
                EnforceDescriptionStandards(optionSetDics);

                //handle parsing errors

                ParsingResult.UnsupportedOps = optionsNotSupportedByAnyDictionary;
                if (ParsingResult.UnsupportedOps != null && ParsingResult.UnsupportedOps.Count > 0)
                {
                    ParsingResult.UpdateExitCode(ExitCodeType.UnknownCommandLineOption);
                    ParsingResult.ShowHelpMenu = true;
                    var unsupportedOpsString = string.Join(",", ParsingResult.UnsupportedOps);
                    ParsingResult.Exception = new ArgumentException("Unsupported arguments detected:" + unsupportedOpsString);
                    CommandLineUtilities.ShowUnsupportedOptions(ParsingResult.UnsupportedOps);
                }

                if (ParsingResult.ShowHelpMenu || ParsingResult.ShowVersion)
                    return;

                if (includeValidationWithParsing) //note we only skip validation during testing, when we test the parsing  independent of the validation steps
                {
                    CheckForRequiredOptions();

                    //validate requested options make sense
                    if (HadSuccess)
                        ValidateOptions();
                }



            }
            catch (Exception e)
            {
                ParsingResult.ErrorBuilder.AppendFormat("{0}ERROR: {1}\n", ParsingResult.ErrorSpacer, e.Message);
                ParsingResult.UpdateExitCode(ExitCodeType.BadArguments); //better than "UnknownCommandLineOption"
                ParsingResult.ShowHelpMenu = true;
                ParsingResult.Exception = e;

                ExitCodeUtilities.ShowExceptionAndUpdateExitCode(e);

            }
            
        }

        public void EnforceDescriptionStandards(Dictionary<string, OptionSet> optionSetDics)
        {
            foreach (var key in optionSetDics.Keys)
            {
                foreach (var optSet in optionSetDics[key])
                {
                    if (string.IsNullOrEmpty(optSet.Description))
                        throw new NotSupportedException("Please write a description for the following option: " + string.Join(',', optSet.Names));

                    if ((optSet.ToString().Contains("=")) && !(optSet.Description.Contains("{")))
                        throw new NotSupportedException("Please add the type to the description of the following option: " + string.Join(',', optSet.Names));

                }

            }
        }

        /// <summary>
        /// checks if an input directory exists
        /// </summary>
        protected void CheckDirectoryContainsFiles(string directoryPath, string description, string commandLineOption,
            string searchPattern)
        {
            if (!Directory.Exists(directoryPath)) return;

            var files = Directory.GetFiles(directoryPath, searchPattern);
            if (files.Length != 0) return;

            ParsingResult.ErrorBuilder.Append($"{ ParsingResult.ErrorSpacer}ERROR: The {description} directory ({directoryPath}) does not contain the required files ({searchPattern}). Please use the {commandLineOption} parameter.\n");
            ParsingResult.UpdateExitCode(ExitCodeType.FileNotFound);
        }

        protected void CheckAndCreateDirectory(string directoryPath, string description, string commandLineOption, bool isRequired = true)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                if (isRequired)
                {
                    ParsingResult.ErrorBuilder.AppendFormat("{0}ERROR: The {1} directory was not specified. Please use the {2} parameter.\n", ParsingResult.ErrorSpacer, description, commandLineOption);
                    ParsingResult.UpdateExitCode(ExitCodeType.MissingCommandLineOption);
                }
            }
            else if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }



        /// <summary>
        /// checks if an input file exists
        /// </summary>
        protected void CheckInputFilenameExists(string filePath, string description, string commandLineOption, bool isRequired = true)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                if (isRequired)
                {
                    ParsingResult.ErrorBuilder.AppendFormat("{0}ERROR: The {1} file was not specified. Please use the {2} parameter.\n", ParsingResult.ErrorSpacer, description, commandLineOption);
                    ParsingResult.UpdateExitCode(ExitCodeType.MissingCommandLineOption);
                }
            }
            else if (!File.Exists(filePath))
            {
                ParsingResult.ErrorBuilder.AppendFormat("{0}ERROR: The {1} file ({2}) does not exist.\n", ParsingResult.ErrorSpacer, description, filePath);
                ParsingResult.UpdateExitCode(ExitCodeType.FileNotFound);
            }
        }

        /// <summary>
        /// checks if the required parameter has been set
        /// </summary>
        protected void HasRequiredParameter<T>(T parameterValue, string description, string commandLineOption)
        {
            if (EqualityComparer<T>.Default.Equals(parameterValue, default(T)))
            {
                ParsingResult.ErrorBuilder.AppendFormat("{0}ERROR: The {1} was not specified. Please use the {2} parameter.\n", ParsingResult.ErrorSpacer, description, commandLineOption);
                ParsingResult.UpdateExitCode(ExitCodeType.MissingCommandLineOption);
            }
        }

       

        public void CheckForRequiredOptions()
        {
            var missingOptions = GetMissingRequiredOptions();

            foreach (var missingOption in missingOptions)
            {
                ParsingResult.ErrorBuilder.AppendFormat("{0}ERROR: The {1} was not specified. Please use the {2} parameter. Description: {3} \n", 
                    ParsingResult.ErrorSpacer, 
                    missingOption.ToString(), missingOption.ToString(), missingOption.Description);

                ParsingResult.UpdateExitCode(ExitCodeType.MissingCommandLineOption);
            }

            ParsingResult.Exception = new ArgumentException(ParsingResult.ErrorBuilder.ToString());
        }

        public List<Option> GetMissingRequiredOptions()
        {
            var missingOptions = new List<Option>();

            foreach (var option in OptionSetDics[OptionSetNames.Required])
            {

                bool found = false;
                var names = option.Names;

                foreach (var name in names)
                {

                    foreach (var command in CommandLineArguments)
                    {
                        //these are values, not options
                        if (String.IsNullOrEmpty(command) || (command[0] != '-'))
                            continue;

                        //now we have a real option
                        if ((command.ToLower().Trim('-') == name.ToLower()))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found == true)
                        break;
                }

                if (!found)
                    missingOptions.Add(option);
        
            }

            return missingOptions;
        }
    }
    }