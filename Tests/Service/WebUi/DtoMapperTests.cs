using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Service.WebUi
{
    [TestClass]
    public class DtoMapperTests
    {
        // 纯函数测试，不需要 TestInit/IoC
        [TestMethod]
        public void Map_RdpServer_FieldsComplete()
        {
            var rdp = new RDP
            {
                Id = "01J8Z",
                DisplayName = "WIN-PROD-01",
                Address = "192.168.1.10",
                Port = "3389",
            };
            rdp.Tags = new List<string> { "生产环境", "monitor" };
            rdp.ColorHex = "#2c5aff";
            rdp.TreeNodes = new List<string> { "生产环境", "Web 集群" };

            var dto = _1RM.Service.WebUi.DtoMapper.FromServer(rdp, dataSourceName: "Local");

            Assert.AreEqual("01J8Z", dto.Id);
            Assert.AreEqual("WIN-PROD-01", dto.DisplayName);
            Assert.AreEqual("RDP", dto.Protocol);
            Assert.AreEqual("192.168.1.10", dto.Address);
            Assert.AreEqual(2, dto.Tags.Count);
            Assert.AreEqual("#2c5aff", dto.Color);
            Assert.AreEqual("生产环境/Web 集群", dto.FolderPath);
            Assert.AreEqual("Local", dto.DataSourceName);
            Assert.AreEqual("disconnected", dto.ConnectionState); // 预留字段默认值
            Assert.AreEqual(0L, dto.LastConnectTime); // Task 4 从 LocalityConnectRecorder 填充
        }

        [TestMethod]
        public void Map_NullTreeNodes_RootFolder()
        {
            var ssh = new SSH { Id = "x", DisplayName = "h", Address = "1.1.1.1" };
            var dto = _1RM.Service.WebUi.DtoMapper.FromServer(ssh, "Local");
            Assert.AreEqual(string.Empty, dto.FolderPath);
        }

        [TestMethod]
        public void Map_ProtocolWithoutAddressPort_EmptyConnFields()
        {
            // 列表中可能存在不继承 ProtocolBaseWithAddressPort 的协议（如 Dummy），
            // 映射必须不抛异常且连接字段为空串
            var dummy = new Dummy { Id = "d", DisplayName = "group-header" };
            var dto = _1RM.Service.WebUi.DtoMapper.FromServer(dummy, "Local");
            Assert.AreEqual(string.Empty, dto.Address);
            Assert.AreEqual(string.Empty, dto.Port);
            Assert.AreEqual(string.Empty, dto.UserName);
            Assert.AreEqual("group-header", dto.DisplayName);
        }
    }
}
