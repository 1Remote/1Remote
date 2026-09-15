using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// 连接编辑器的编排逻辑（供 Web UI 端点复用，与 HTTP 层解耦；Task: create/update/delete/batch 同此后继）。
    /// 加密纪律：VmItemList 内存缓存中的对象是加密态（DB 读路径不落解密），
    /// 一切对外的明文视图必须建立在 Clone() 副本上——WPF 编辑器对缓存原地解密是既有缺陷，Web 不复制。
    /// </summary>
    public static class WebUiEditorService
    {
        /// <summary>
        /// 取服务器的可编辑配置：克隆 + 解密后的全字段明文 JSON（PascalCase 直通）。
        /// 未找到或不可编辑（Dummy 分组头/临时会话）返回 null，由端点转 404。
        /// 返回的 json 可直接经 ItemCreateHelper.CreateFromJsonString 反序列化
        /// （Protocol/ClassVersion 鉴别字段在文中，访问大小写敏感，勿做命名转换）。
        /// </summary>
        public static string? GetEditableConfig(string dataSourceName, string id)
        {
            var gd = IoC.Get<GlobalData>();
            ProtocolBaseViewModel? vm;
            lock (gd) // 快照语义同 /api/servers：锁内只做查找，克隆/解密/序列化在锁外
            {
                vm = gd.GetItemById(dataSourceName, id);
            }
            if (vm == null || !WebUiEndpoints.IsConnectable(vm.Server))
                return null;

            // 必须先克隆后解密：缓存中的 Server 为加密态，原地解密会污染缓存
            // （后续读到的 Password 变明文、加密往返语义破坏），WPF 的原地解密是既有缺陷。
            var clone = (ProtocolBase)vm.Server.Clone();
            clone.DecryptToConnectLevel();
            return clone.ToJsonString();
        }
    }
}
