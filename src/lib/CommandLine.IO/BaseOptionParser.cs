using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CommandLine.NDesk.Options;
using CommandLine.IO.Utilities;

namespace CommandLine.IO
{


    public abstract class BaseOptionParser
    {

        public CommandLineParseResult ParsingResult = new CommandLineParseResult();
        public Dictionary<string, OptionSet> OptionSetDics;
        
        public string[] CommandLineArguments;

        public abstract void ValidateOptions();


        public abstract Dictionary<string, OptionSet> GetParsingMethods();


        public void ParseArgs(string[] args, bool includeValidation = true)
        {
            CommandLineArguments = args;
            var dictionary = GetParsingMethods();
            AddBasicParsing(dictionary, ParsingResult);
            DoGenericParsingAndValidation(args, dictionary, includeValidation);
        }

        public bool HadSuccess { get => ParsingResult.ExitCode==  (int) ExitCodeType.Success; }
        public bool ParsingFailed { get => !HadSuccess; }


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

                foreach (var ops in optionSetDics)
                {
                    var currentUnspportedOps = ops.Value.Parse(args);

                    //sanitize
                    currentUnspportedOps.RemoveAll((c) => c == "");
                    currentUnspportedOps.RemoveAll((c) => c[0] != '-');

                    if (optionsNotSupportedByAnyDictionary != null)
                        optionsNotSupportedByAnyDictionary = optionsNotSupportedByAnyDictionary.Intersect(currentUnspportedOps).ToList();

                    else
                        optionsNotSupportedByAnyDictionary = currentUnspportedOps;

                }

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
                ParsingResult.UpdateExitCode(ExitCodeType.UnknownCommandLineOption);
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
                ParsingResult.ErrorBuilder.AppendFormat("{0}ERROR: The {1} was not specified. Please use the {2} parameter. Descirption: {3} \n", 
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