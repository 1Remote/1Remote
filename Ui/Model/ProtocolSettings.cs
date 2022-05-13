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
        private string _selectedRunnerName = "";
        public string SelectedRunnerName
        {
            get
            {
                if (Runners.Count > 0 && Runners.All(x => x.Name != _selectedRunnerName))
                {
                    return Runners.First().Name;
                }
                return _selectedRunnerName;
            }
            set
            {
                if (Runners.Any(x => x.Name == value))
                {
                    _selectedRunnerName = value;
                }
            }
        }
        public List<Runner> Runners { get; set; } = new List<Runner>();



        [JsonIgnore]
        public Type ProtocolType { get; private set; } = typeof(RDP);

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

        public void Init(List<string> marcoNames, List<string> marcoDescriptions, Type protocolType)
        {
            MarcoNames = marcoNames;
            MarcoDescriptions = marcoDescriptions;
            ProtocolType = protocolType;
        }
    }
}
