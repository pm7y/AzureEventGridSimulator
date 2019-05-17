using AzureEventGridSimulator;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using NUnit.Framework;

namespace Tests
{
    public class SimpleFilterEventAcceptanceTests
    {
        [Test]
        public void TestDefaultFilterSettingsAcceptsDefaultGridEvent()
        {
            var filterConfig = new FilterSetting();
            var gridEvent = new EventGridEvent();
            Assert.True(filterConfig.AcceptsEvent(gridEvent));
        }

        [TestCase(arg: null)]
        [TestCase(arg: new string[] { "All" })]
        [TestCase(arg: new string[] { "This.is.a.test" })]
        public void TestEventTypeFilteringSuccess(string[] includedEventTypes)
        {
            var filterConfig = new FilterSetting { IncludedEventTypes = includedEventTypes };
            var gridEvent = new EventGridEvent { EventType = "This.is.a.test" };
            Assert.True(filterConfig.AcceptsEvent(gridEvent));
        }

        [TestCase(arg: new string[] { "This" })]
        [TestCase(arg: new string[] { "this.is.a.test" })]
        [TestCase(arg: new string[] { "THIS.IS.A.TEST" })]
        [TestCase(arg: new string[] { "this.is.a.test.event" })]
        [TestCase(arg: new string[] { "this.is.a.testevent" })]
        [TestCase(arg: new string[0])]
        public void TestEventTypeFilteringFailure(string[] includedEventTypes)
        {
            var filterConfig = new FilterSetting { IncludedEventTypes = includedEventTypes };
            var gridEvent = new EventGridEvent { EventType = "This.is.a.test" };
            Assert.False(filterConfig.AcceptsEvent(gridEvent));
        }

        [TestCase("This", null, true)]
        [TestCase("This", null, false)]
        [TestCase("THIS", null, false)]
        [TestCase("this_is_a_test_subject", null, false)]
        [TestCase("This_Is_A_Test_", null, true)]
        [TestCase("T", null, true)]
        [TestCase("t", null, false)]
        [TestCase("", null, true)]
        [TestCase("", null, false)]
        [TestCase(null, null, false)]
        [TestCase(null, null, true)]
        [TestCase("This", "ect", true)]
        [TestCase("This", "eCt", false)]
        [TestCase("this_is_a_test_subject", "this_is_a_test_subject", false)]
        [TestCase("This_Is_A_Test_Subject", "This_Is_A_Test_Subject", true)]
        [TestCase(null, "This_Is_A_Test_Subject", true)]
        [TestCase(null, "_Subject", true)]
        [TestCase(null, "_subject", false)]
        [TestCase(null, "_SUBJECT", false)]
        public void TestSubjectFilteringSuccess(string beginsWith, string endsWith, bool caseSensitive)
        {
            var filterConfig = new FilterSetting { SubjectBeginsWith = beginsWith, SubjectEndsWith = endsWith, IsSubjectCaseSensitive = caseSensitive };
            var gridEvent = new EventGridEvent { Subject = "This_Is_A_Test_Subject" };
            Assert.True(filterConfig.AcceptsEvent(gridEvent));
        }

        [TestCase("Thus", null, true)]
        [TestCase("Thus", null, false)]
        [TestCase("TH", null, true)]
        [TestCase("wrong", null, false)]
        [TestCase("t", null, true)]
        [TestCase("This", "wrong", false)]
        [TestCase("This", "eCt", true)]
        [TestCase("this_is_a_test_subject", "this_is_a_test_subject", true)]
        [TestCase("This_Is_A_Test_Subject", "wrong", true)]
        [TestCase(null, "THIS_IS_A_TEST_SUBJECT", true)]
        [TestCase(null, "this_is_a_test_subject", true)]
        [TestCase(null, "_subject", true)]
        [TestCase(null, "_SUBJECT", true)]
        public void TestSubjectFilteringFailure(string beginsWith, string endsWith, bool caseSensitive)
        {
            var filterConfig = new FilterSetting { SubjectBeginsWith = beginsWith, SubjectEndsWith = endsWith, IsSubjectCaseSensitive = caseSensitive };
            var gridEvent = new EventGridEvent { Subject = "This_Is_A_Test_Subject" };
            Assert.False(filterConfig.AcceptsEvent(gridEvent));
        }
    }
}
