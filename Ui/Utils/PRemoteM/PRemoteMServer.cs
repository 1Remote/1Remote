using _1RM.Model.DAO;
using _1RM.Model.Protocol.Base;
using Newtonsoft.Json.Linq;

namespace _1RM.Utils.PRemoteM;

internal class PRemoteMServer : IDataBaseServer
{
    public int Id { get; set; }
    public string Protocol { get; set; } = "";
    public string ClassVersion { get; set; } = "";
    public string JsonConfigString { get; set; } = "";

    public ProtocolBase? ToProtocolServerBase()
    {
        var x = ItemCreateHelper.CreateFromDbOrm(this);
        if (string.IsNullOrEmpty(x.DisplayName))
        {
            // 尝试从更老的版本读取 DispName
            var o = JObject.Parse(this.JsonConfigString);
            if (o.Property("DispName") != null)
            {
                x.DisplayName = ((string?)(o["DispName"])) ?? "";
            }
            if (o.Property("GroupName") != null && x.Tags.Count == 0)
            {
                var tag = ((string?)(o["DispName"])) ?? "";
                if (string.IsNullOrEmpty(tag))
                {
                    x.Tags.Add(tag);
                }
            }
        }
        return x;
    }

    public string GetId()
    {
        return Id.ToString();
    }

    public string GetProtocol()
    {
        return Protocol;
    }

    public string GetClassVersion()
    {
        return ClassVersion;
    }

    public string GetJson()
    {
        return JsonConfigString;
    }
}