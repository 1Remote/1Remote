using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.Protocol;

namespace PRM.Core.DB.IDB
{
    public interface IDbServer
    {
        int GetId();

        string GetProtocol();

        string GetClassVersion();

        string GetJson();

        string GetUpdatedMark();

        ProtocolServerBase ToProtocolServerBase();
    }
}