using System.Collections.Generic;
using _1RM.Model.Protocol.Base;
using _1RM.View;

namespace _1RM.Service.DataSource;

public interface IDataSource : IDataService
{
    /// <summary>
    /// 已缓存的服务器信息
    /// </summary>
    public List<ProtocolBaseViewModel> CachedProtocols { get; }

    /// <summary>
    /// 返回数据源的 ID 
    /// </summary>
    /// <returns></returns>
    public string GetDataSourceId();

    /// <summary>
    /// 返回服务器信息(服务器信息已指向数据源)
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ProtocolBaseViewModel> GetServers();

    public bool IsReadable();
    public bool IsWritable();

    public long LastUpdateTimestamp { get; }
    public long DataSourceUpdateTimestamp { get; set; }

    /// <summary>
    /// 定期检查数据源的最后更新时间戳，大于 LastUpdateTimestamp 则返回 true
    /// </summary>
    /// <returns></returns>
    public bool NeedReload();
}