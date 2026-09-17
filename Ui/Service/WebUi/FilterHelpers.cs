using System.Collections.Generic;
using System.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// 服务端服务器过滤：/api/search 与 WPF 主窗口共用同一套过滤语义。
    /// 匹配核心直接复用 <see cref="TagAndKeywordEncodeHelper"/>（#tag 解码 + 多关键字匹配，
    /// 内部经 IoC 使用 <c>KeywordMatchService</c>，拼音/首字母等能力随应用配置自动生效），
    /// 与 ServerPageViewModelBase.CalcServerVisibleAndRefresh 的过滤路径一致。
    /// 该 VM 方法额外耦合 UI 状态（MainFilterString、IsServerVisible 增量缓存、TagFilters 面板），
    /// 故此处只提供无 UI 依赖的纯匹配入口，不改动 VM。
    /// </summary>
    public static class FilterHelpers
    {
        /// <summary>
        /// 按关键字过滤服务器列表，语义与主窗口过滤框一致：
        /// 空白关键字返回全部；"#tag"（含 -# 排除与前缀补全）与空格分隔的多关键字全部参与匹配。
        /// </summary>
        /// <param name="source">待过滤列表（调用方负责在锁内快照）</param>
        /// <param name="keyword">过滤串，同主窗口输入，如 "#prod web"；null 视为空</param>
        /// <param name="matchSubTitle">是否同时匹配副标题（与 VM 调用默认值一致）</param>
        /// <returns>命中的服务器，保持源顺序</returns>
        public static List<ProtocolBaseViewModel> MatchServers(List<ProtocolBaseViewModel> source, string? keyword, bool matchSubTitle = true)
        {
            var decoded = TagAndKeywordEncodeHelper.DecodeKeyword(keyword ?? string.Empty);
            var servers = source.Select(x => x.Server).ToList();
            var matchResults = TagAndKeywordEncodeHelper.MatchKeywords(servers, decoded, matchSubTitle);

            // MatchKeywords 按输入顺序返回每台服务器的 (是否命中, 高亮结果)；计数不一致时按较短侧截断
            var matched = new List<ProtocolBaseViewModel>(matchResults.Count);
            for (var i = 0; i < source.Count && i < matchResults.Count; i++)
            {
                if (matchResults[i].Item1)
                {
                    matched.Add(source[i]);
                }
            }
            return matched;
        }
    }
}
