using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PRM.Model.Protocol;
using PRM.Model.ProtocolRunner;

namespace PRM.Model
{
    public class ProtocolSettings
    {
        public string SelectedRunnerName { get; set; } = "";
        public List<Runner> Runners { get; set; } = new List<Runner>();


        /// <summary>
        /// All macros name
        /// </summary>
        [JsonIgnore]
        public List<string> MarcoNames { get; private set; } = new List<string>();
        /// <summary>
        /// All macros Descriptions
        /// </summary>
        [JsonIgnore]
        public List<string> MarcoDescriptions { get; private set; } = new List<string>();

        /// <summary>
        /// Descriptions like:
        /// What A is -> %MarcoName1%
        /// What B is -> %MarcoName2%
        /// </summary>
        [JsonIgnore]
        public string GetAllDescriptions
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < MarcoNames.Count; i++)
                {
                    sb.AppendLine($@"%{MarcoDescriptions[i]}%      ->      {MarcoNames[i]}");
                }
                return sb.ToString();
            }
        }

        public ProtocolSettings()
        {
        }

        public void Init(List<string> marcoNames, List<string> marcoDescriptions)
        {
            MarcoNames = marcoNames;
            MarcoDescriptions = marcoDescriptions;
        }
    }
}
