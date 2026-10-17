using System.Collections.Generic;
using _1RM.Model.Protocol;
using Newtonsoft.Json;
using Xunit;

namespace Tests.Model
{
    /// <summary>
    /// The Selections setter used to reset Value to the LAST option unconditionally.
    /// Value is assigned before Selections when deserializing, so every db read wiped the stored selection.
    /// </summary>
    public class AppArgumentSelectionTests
    {
        private static AppArgument DeserializeFromStoredJson()
        {
            // property order mirrors the model's declaration order, i.e. what
            // JsonConvert.SerializeObject produces and what sits in the database:
            // Type, IsNullable, ..., Value, ..., Selections
            var json = "{\"Type\":\"Selection\",\"IsNullable\":false,\"Name\":\"mode\"," +
                       "\"Value\":\"opt1\"," +
                       "\"Selections\":{\"opt1\":\"Fast\",\"opt2\":\"Slow\",\"opt3\":\"Slowest\"}}";
            return JsonConvert.DeserializeObject<AppArgument>(json);
        }

        [Fact]
        public void Selections_Deserialization_MustKeepStoredValue()
        {
            var arg = DeserializeFromStoredJson();
            Assert.True(arg.Value == "opt1",
                $"the stored selection was reset to '{arg.Value}' while deserializing");
        }

        [Fact]
        public void Selections_EmptyValue_OnNewArgument_StillDefaultsToLastOption()
        {
            var arg = new AppArgument { Type = AppArgumentType.Selection, IsNullable = false };
            arg.Selections = new Dictionary<string, string> { { "opt1", "A" }, { "opt2", "B" } };
            Assert.Equal("opt2", arg.Value);
        }

        [Fact]
        public void Selections_StaleValue_AfterOptionsChanged_StillFallsBackToLastOption()
        {
            var arg = new AppArgument { Type = AppArgumentType.Selection, IsNullable = false };
            arg.Value = "removed-option";
            arg.Selections = new Dictionary<string, string> { { "opt1", "A" }, { "opt2", "B" } };
            Assert.Equal("opt2", arg.Value);
        }

        [Fact]
        public void Selections_NullableEmptyValue_IsPreservedAsTheBlankOption()
        {
            var arg = new AppArgument { Type = AppArgumentType.Selection, IsNullable = true };
            arg.Value = "";
            arg.Selections = new Dictionary<string, string> { { "opt1", "A" } };
            Assert.Equal("", arg.Value);
        }
    }
}
