using System;
using System.Collections.Generic;
using Xunit;


namespace Psara.Tests
{
    public class GeometricFilterParameterTests
    {


        [Fact]
        public void UnSupportedOptions()
        {
            //deliberate fails 
            PsaraSettingsTests.ExecuteParsingTest("-inclusionmodel byOverlap", false);
            PsaraSettingsTests.ExecuteParsingTest("-i intervalfile", false);
            PsaraSettingsTests.ExecuteParsingTest("-RIO wrongname", false);
        }

        [Fact]
        /// <summary>
        ///Just test the enum options, Everything else was well tested through Psara settings tests
        /// </summary>
        public void InclusionModelOptionsTest()
        {
            
            var optionExpectations = ByStartPos();
            Action<PsaraOptions> expectations = null;
            foreach (var option in optionExpectations.Values)
            {
                expectations += option;
            }

            PsaraSettingsTests.ExecuteParsingTest(string.Join(" ", optionExpectations.Keys), true, expectations);


            optionExpectations = ByExpanded();
            expectations = null;
            foreach (var option in optionExpectations.Values)
            {
                expectations += option;
            }

            PsaraSettingsTests.ExecuteParsingTest(string.Join(" ", optionExpectations.Keys), true, expectations);

        }

        private Dictionary<string, Action<PsaraOptions>> ByStartPos()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<PsaraOptions>>();
            optionsExpectationsDict.Add("--inclusionmodel start", (o) => Assert.Equal(GeometricFilterParameters.InclusionModel.ByStartPosition, o.GeometricFilterParameters.InclusionStrategy));
            return optionsExpectationsDict;
        }

        private Dictionary<string, Action<PsaraOptions>> ByExpanded()
        {
            var optionsExpectationsDict = new Dictionary<string, Action<PsaraOptions>>();
            optionsExpectationsDict.Add("--inclusionmodel Expand", (o) => Assert.Equal(GeometricFilterParameters.InclusionModel.Expanded, o.GeometricFilterParameters.InclusionStrategy));
            return optionsExpectationsDict;
        }
    }
}