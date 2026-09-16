using System;
using System.Collections.Generic;
using System.IO;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.View.ServerView;
using Shawn.Utils.Interface;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYin;
using VariableKeywordMatcher.Provider.ChineseZhCnPinYinInitials;
using VariableKeywordMatcher.Provider.DirectMatch;

namespace Tests
{
    /// <summary>
    /// Web UI 端点集成测试夹具：最小 IoC 环境 + 临时 SQLite 本地数据源 + 种子服务器。
    /// 注意：IoC.GetByType 是直通委托（Ui/Ioc.cs），这里用缓存实例表替换，
    /// 保证 IoC.Get&lt;T&gt; 每次返回同一实例——否则种子数据与端点内取到的不是同一对象。
    /// </summary>
    public static class TestInit
    {
        private static readonly object Gate = new object();
        private static bool _initialized = false;
        private static readonly Dictionary<Type, object> Instances = new Dictionary<Type, object>();
        private static readonly MockLanguageService LanguageService = new MockLanguageService();

        public static void Init()
        {
            lock (Gate)
            {
                if (_initialized) return;
                _initialized = true;

                // 协议 VM 构造走 Execute.OnUIThreadSync（DataSourceBase.GetServers），
                // 测试环境无 WPF Application，强制同步派发以在任意测试线程可用
                Stylet.Execute.Dispatcher = Stylet.SynchronousDispatcher.Instance;

                _1RM.IoC.GetByType = (type, key) =>
                {
                    if (Instances.TryGetValue(type, out var obj)) return obj;
                    if (type == typeof(ILanguageService)) return LanguageService;
                    return null;
                };

                // 临时库：需预先创建空文件（SqliteSource.Path setter 会读 FileInfo.IsReadOnly，
                // 文件不存在会抛 FileNotFoundException）
                var dbPath = Path.Combine(Path.GetTempPath(), "tests-1rm-webui.db");
                foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                {
                    try
                    {
                        if (File.Exists(f)) File.Delete(f);
                    }
                    catch (IOException)
                    {
                        // 被占用则沿用旧库，种子插入按主键幂等失败但列表仍含种子
                    }
                }
                if (!File.Exists(dbPath))
                {
                    File.WriteAllText(dbPath, string.Empty);
                }

                // 1. 注册服务（构造 GlobalData 需要 ConfigurationService）
                var keywordMatchService = new KeywordMatchService();
                Register(keywordMatchService);

                var configuration = new Configuration
                {
                    DatabaseCheckPeriod = 0, // 关闭 GlobalData 重载定时器，避免测试期后台 tick
                    SqliteDatabasePath = dbPath,
                    // 固定启用拼音 provider：ConfigurationService 构造时会按本配置调用
                    // KeywordMatchService.Init（EnabledMatchers 为空时回退到按当前 locale 决定，
                    // 会导致拼音断言在非 zh 环境下失败），这里显式固定，保证跨 locale 确定
                    KeywordMatch = new KeywordMatchConfig
                    {
                        EnabledMatchers = new List<string>
                        {
                            DirectMatchProvider.GetName(),
                            ChineseZhCnPinYinMatchProvider.GetName(),
                            ChineseZhCnPinYinInitialsMatchProvider.GetName(),
                        },
                    },
                };
                var configurationService = new ConfigurationService(keywordMatchService, configuration);
                Register(configurationService);

                var dataSourceService = new DataSourceService();
                Register(dataSourceService);

                var globalData = new GlobalData(configurationService);
                Register(globalData);

                // 运行器读写端点（/api/settings/runners）需要 ProtocolConfigurationService。
                // PuttyRunner 构造依赖 WPF pack 资源（PuttyThemes：pack://application URI 在测试宿主
                // 不可用，UriFormatException）——预置主题缓存占位（SetBrush 对空条目安全），绕开资源装载。
                var themesField = typeof(_1RM.Utils.PuTTY.PuttyThemes).GetField("_themes",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                themesField?.SetValue(null,
                    new Dictionary<string, List<_1RM.Utils.PuTTY.Model.PuttyConfigKeyValuePair>>
                    {
                        { "Default", new List<_1RM.Utils.PuTTY.Model.PuttyConfigKeyValuePair>() },
                    });
                Register(new ProtocolConfigurationService());

                // GlobalData.UpdateServer 成功路径会调用 IoC.Get<ServerListPageViewModel>().ClearSelection()
                // （编辑保存后清除列表选中态），测试环境必须注册同型实例；其构造仅依赖上述已注册服务，
                // View 永不附加（OnViewLoaded 不触发），ClearSelection 在未构建的空列表上是空操作。
                Register(new TagsPanelViewModel()); // ServerListPageViewModel 基类构造需要
                Register(new ServerListPageViewModel(dataSourceService, globalData));

                // 2. 数据链路：构造 DataSourceService → InitLocalDataSource → SetDataSourceService → ReloadAll → AddServer
                var localSource = new SqliteSource(DataSourceService.LOCAL_DATA_SOURCE_NAME) { Path = dbPath };
                dataSourceService.InitLocalDataSource(localSource);

                globalData.SetDataSourceService(dataSourceService);
                globalData.ReloadAll(true);

                globalData.AddServer(new RDP
                {
                    Id = "seed-rdp",
                    DisplayName = "seed-rdp",
                    Address = "1.1.1.1",
                    Tags = new List<string> { "seed-tag" }, // /api/tags 断言用
                }, localSource);
            }
        }

        private static void Register<T>(T instance) where T : class
        {
            Instances[typeof(T)] = instance;
        }
    }
}
