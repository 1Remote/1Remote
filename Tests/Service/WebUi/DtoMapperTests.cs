using System;
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
            Assert.AreEqual(0L, dto.LastConnectTime); // 缺省 0；真实列表由调用方从 LocalityConnectRecorder 传入
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

        [TestMethod]
        public void Map_TelnetMiddleLayer_HasAddressPortButNoUserName()
        {
            // Telnet 继承 ProtocolBaseWithAddressPort（中间层）：有地址端口，无用户名
            var telnet = new Telnet { Id = "tel-1", DisplayName = "sw-01", Address = "10.0.0.8" };
            var dto = _1RM.Service.WebUi.DtoMapper.FromServer(telnet, "Local");
            Assert.AreEqual("10.0.0.8", dto.Address);
            Assert.AreEqual("23", dto.Port); // Telnet 构造时的默认端口
            Assert.AreEqual(string.Empty, dto.UserName);
        }

        [TestMethod]
        public void Map_TagsIsSnapshot_NotAffectedByLaterMutation()
        {
            // DTO.Tags 是映射时刻的快照，之后修改协议对象的 Tags 不应影响已映射结果
            var rdp = new RDP { Id = "snap", DisplayName = "snap", Address = "1.1.1.1" };
            rdp.Tags = new List<string> { "a" };
            var dto = _1RM.Service.WebUi.DtoMapper.FromServer(rdp, "Local");
            Assert.AreEqual(1, dto.Tags.Count);

            rdp.Tags.Add("b");
            Assert.AreEqual(1, dto.Tags.Count, "dto.Tags 不应随源对象变化");
        }

        [TestMethod]
        public void Map_LastConnectTime_ConvertsToUnixSeconds()
        {
            var rdp = new RDP { Id = "lc", DisplayName = "lc", Address = "1.1.1.1" };
            var dto = _1RM.Service.WebUi.DtoMapper.FromServer(rdp, "Local");
            Assert.AreEqual(0L, dto.LastConnectTime); // 默认参数(未连接)映射为 0

            var time = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Local);
            var dto2 = _1RM.Service.WebUi.DtoMapper.FromServer(rdp, "Local", time);
            Assert.AreEqual(new DateTimeOffset(time).ToUnixTimeSeconds(), dto2.LastConnectTime);
        }

        [TestMethod]
        public void Map_Note_EmptyAndNonEmpty()
        {
            // 列表行备注悬停预览需要 DTO 携带 Note（Markdown 源文本）。
            // 未设置时 ProtocolBase.Note 默认空串 → DTO 空串（前端以空判隐藏备注图标）
            var noNote = new RDP { Id = "n0", DisplayName = "no-note", Address = "1.1.1.1" };
            var dto0 = _1RM.Service.WebUi.DtoMapper.FromServer(noNote, "Local");
            Assert.AreEqual(string.Empty, dto0.Note);

            var withNote = new RDP { Id = "n1", DisplayName = "with-note", Address = "1.1.1.1" };
            withNote.Note = "**prod** jump host\n- idrac: 10.0.0.9";
            var dto1 = _1RM.Service.WebUi.DtoMapper.FromServer(withNote, "Local");
            Assert.AreEqual("**prod** jump host\n- idrac: 10.0.0.9", dto1.Note);
        }
    }
}
