using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PRM.Core;
using PRM.Core.Model;
using PRM.Core.Protocol;
using Shawn.Utils;

using Shawn.Utils;

namespace PRM.ViewModel
{
    public class VmServerEditorPage : NotifyPropertyChangedBase
    {
        private readonly PrmContext _context;

        /// <summary>
        /// to remember original protocol options
        /// </summary>
        private readonly ProtocolServerBase _orgServerBase;

        public VmServerEditorPage(PrmContext context, ProtocolServerBase server, bool isDuplicate = false)
        {
            _context = context;
            _orgServerBase = server;
            Server = (ProtocolServerBase)server.Clone();
            _isDuplicate = isDuplicate;
            if (_isDuplicate)
                Server.Id = 0;
            IsAddMode = Server.Id <= 0;

            // decrypt pwd
            _context.DbOperator.DecryptPwdIfItIsEncrypted(Server);

            var assembly = typeof(ProtocolServerBase).Assembly;
            var types = assembly.GetTypes();
            // reflect remote protocols
            {
                ProtocolList.Clear();
                ProtocolList = types.Where(item => item.IsSubclassOf(typeof(ProtocolServerBase)) && !item.IsAbstract)
                    .Select(type => (ProtocolServerBase)Activator.CreateInstance(type)).OrderBy(x => x.GetListOrder()).ToList();
            }

            // set selected protocol
            try
            {
                ProtocolSelected = ProtocolList.First(x => x.GetType() == Server.GetType());
            }
            catch (Exception)
            {
                ProtocolSelected = ProtocolList.First();
            }

            NameSelections = _context.AppData.VmItemList.Select(x => x.Server.DispName)
                .Distinct()
                .Where(x => !string.IsNullOrEmpty(x)).ToList();

            GroupSelections = _context.AppData.VmItemList.Select(x => x.Server.GroupName)
                .Distinct()
                .Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        private ProtocolServerBase _server = null;

        public ProtocolServerBase Server
        {
            get => _server;
            set => SetAndNotifyIfChanged(nameof(Server), ref _server, value);
        }

        private ProtocolServerBase _protocolSelected = null;

        public ProtocolServerBase ProtocolSelected
        {
            get => _protocolSelected;
            set
            {
                if (value != _protocolSelected)
                {
                    SetAndNotifyIfChanged(nameof(ProtocolSelected), ref _protocolSelected, value);
                    if (_orgServerBase.GetType() == Server.GetType())
                        _orgServerBase.Update(Server);
                    ReflectProtocolEditControl();
                }
            }
        }

        private List<ProtocolServerBase> _protocolList = new List<ProtocolServerBase>();

        public List<ProtocolServerBase> ProtocolList
        {
            get => _protocolList;
            set => SetAndNotifyIfChanged(nameof(ProtocolList), ref _protocolList, value);
        }

        private readonly bool _isDuplicate = false;

        private bool _isAddMode = true;

        public bool IsAddMode
        {
            get => _isAddMode;
            set => SetAndNotifyIfChanged(nameof(IsAddMode), ref _isAddMode, value);
        }

        private ProtocolServerFormBase _protocolEditControl = null;

        public ProtocolServerFormBase ProtocolEditControl
        {
            get => _protocolEditControl;
            set => SetAndNotifyIfChanged(nameof(ProtocolEditControl), ref _protocolEditControl, value);
        }

        public List<string> NameSelections { get; set; }
        public List<string> GroupSelections { get; set; }

        private RelayCommand _cmdSave;

        public RelayCommand CmdSave
        {
            get
            {
                if (_cmdSave != null) return _cmdSave;
                _cmdSave = new RelayCommand((o) =>
                {
                    _context.AppData.ServerListUpdate(Server);
                    App.Window.Vm.DispPage = null;
                }, o => (this.Server.DispName.Trim() != "" && (_protocolEditControl?.CanSave() ?? false)));
                return _cmdSave;
            }
        }

        private RelayCommand _cmdCancel;

        public RelayCommand CmdCancel
        {
            get
            {
                if (_cmdCancel != null) return _cmdCancel;
                _cmdCancel = new RelayCommand((o) =>
                {
                    App.Window.Vm.DispPage = null;
                });
                return _cmdCancel;
            }
        }

        private void ReflectProtocolEditControl()
        {
            Debug.Assert(ProtocolSelected != null);
            Debug.Assert(ProtocolSelected.GetType().FullName != null);
            try
            {
                var assembly = typeof(ProtocolServerBase).Assembly;
                // change protocol
                var server = (ProtocolServerBase)assembly.CreateInstance(ProtocolSelected.GetType().FullName);

                // restore original server base info
                if (_orgServerBase.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase))
                    && server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                    server.Update(_orgServerBase, typeof(ProtocolServerWithAddrPortUserPwdBase));
                else if (_orgServerBase.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase))
                         && server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
                    server.Update(_orgServerBase, typeof(ProtocolServerWithAddrPortBase));
                else
                    server.Update(_orgServerBase, typeof(ProtocolServerBase));
                if (_orgServerBase.GetType() == server.GetType())
                    server.Update(_orgServerBase);

                // switch protocol and hold user name & pwd.
                if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase))
                    && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
                    server.Update(Server, typeof(ProtocolServerWithAddrPortUserPwdBase));
                else if (server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase))
                    && Server.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
                    server.Update(Server, typeof(ProtocolServerWithAddrPortBase));
                // switch just hold base info
                else
                    server.Update(Server, typeof(ProtocolServerBase));

                var types = assembly.GetTypes();
                var formName = ProtocolSelected.GetType().Name + "Form";
                var forms = types.Where(x => x.Name == formName).ToList();
                if (forms.Count == 1)
                {
                    var t = forms[0];
                    var parameters = new object[1];
                    parameters[0] = server;
                    ProtocolEditControl = (ProtocolServerFormBase)assembly.CreateInstance(t.FullName, true, System.Reflection.BindingFlags.Default, null, parameters, null, null);
                    Server = server;
                }
                else
                {
                    if (forms.Count == 0)
                        throw new NotImplementedException($"can not find class '{formName}' in {nameof(VmServerEditorPage)}");
                    else
                        throw new Exception($"error on reflecting class '{formName}' in {nameof(VmServerEditorPage)}");
                }
            }
            catch (Exception e)
            {
                SimpleLogHelper.Fatal(e);
                throw;
            }
        }
    }
}