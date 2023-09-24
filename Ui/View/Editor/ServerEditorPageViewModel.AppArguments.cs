using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using _1RM.View.Editor.Forms.Argument;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor
{
    public partial class ServerEditorPageViewModel : NotifyPropertyChangedBase
    {
        private readonly List<Argument> _sharedArgumentsInBuckEdit = new List<Argument>();
        private void AppArgumentsBulkInit(IEnumerable<ProtocolBase> servers)
        {
            // TODO need a remake
            _sharedArgumentsInBuckEdit.Clear();
            var protocolBases = servers as ProtocolBase[] ?? servers.ToArray();
            if (Server is LocalApp app && protocolBases.All(x => x is LocalApp))
            {
                bool isAllTheSameFlag = true;
                var ss = protocolBases.Select(x => (LocalApp)x).ToArray();
                foreach (var s in ss)
                {
                    foreach (var argument in s.ArgumentList)
                    {
                        if (ss.All(x => x.ArgumentList.Any(y => y.IsValueEqualTo(argument) == true)))
                        {
                            if (_sharedArgumentsInBuckEdit.All(x => x.IsValueEqualTo(argument) == false))
                            {
                                _sharedArgumentsInBuckEdit.Add(argument);
                            }
                        }
                        else
                        {
                            isAllTheSameFlag = false;
                        }
                    }
                }

                var list = new List<Argument>();
                if (isAllTheSameFlag == false)
                    list.Add(new Argument(isEditable: false) { Name = Server.ServerEditorDifferentOptions, Value = Server.ServerEditorDifferentOptions });
                list.AddRange(_sharedArgumentsInBuckEdit);
                app.ArgumentList = new ObservableCollection<Argument>(list);
            }
        }
        private void AppArgumentsBulkMerge(IEnumerable<ProtocolBase> servers)
        {
            // TODO need a remake, 编辑时直接修改 _serversInBuckEdit 的值，以避免复杂的合并
            var protocolBases = servers as ProtocolBase[] ?? servers.ToArray();
            if (Server is LocalApp newServer && protocolBases.All(x => x is LocalApp))
            {
                var ss = protocolBases.Select(x => (LocalApp)x).ToArray();
                foreach (var protocolBeforeEdit in ss)
                {
                    foreach (var argument in protocolBeforeEdit.ArgumentList.ToArray())
                    {
                        if (_sharedArgumentsInBuckEdit.Any(x => x.IsValueEqualTo(argument))) // 编辑之前共有，编辑后不再共有，删除，TODO 无法处理改名的情况
                        {
                            if (newServer.ArgumentList.All(x => x.IsValueEqualTo(argument) == false))
                            {
                                protocolBeforeEdit.ArgumentList.Remove(argument);
                            }
                        }
                        else
                        {
                            if (newServer.ArgumentList.All(x => x.Name != Server.ServerEditorDifferentOptions && x.IsEditable != false))
                            {
                                protocolBeforeEdit.ArgumentList.Remove(argument);
                            }
                        }
                    }

                    foreach (var credential in newServer.ArgumentList.Where(x => x.Name != Server.ServerEditorDifferentOptions))
                    {
                        if (protocolBeforeEdit.ArgumentList.All(x => x.IsValueEqualTo(credential) == false))
                        {
                            protocolBeforeEdit.ArgumentList.Add(credential);
                        }
                    }
                }
            }
        }




        private RelayCommand? _cmdEditArgument;
        public RelayCommand CmdEditArgument
        {
            get
            {
                return _cmdEditArgument ??= new RelayCommand((o) =>
                {
                    if (Server is LocalApp protocol)
                    {
                        var arguments = protocol.ArgumentList.ToList();
                        if (IsBuckEdit && _serversInBuckEdit?.Count() > 0)
                        {
                            foreach (var s in _serversInBuckEdit)
                            {
                                if (s is LocalApp p)
                                    arguments.AddRange(p.ArgumentList);
                            }
                        }
                        arguments = arguments.Distinct().ToList();
                        var vm = new ArgumentEditViewModel(protocol, arguments, o as Argument);
                        MaskLayerController.ShowDialogWithMask(vm);
                    }
                }, o => Server is LocalApp);
            }
        }




        private RelayCommand? _cmdDeleteArgument;
        public RelayCommand CmdDeleteArgument
        {
            get
            {
                return _cmdDeleteArgument ??= new RelayCommand((o) =>
                {
                    if (Server is LocalApp protocol && o is Argument org)
                    {
                        if (MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete_selected"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                        {
                            if (protocol.ArgumentList.Contains(org) == true)
                            {
                                protocol.ArgumentList.Remove(org);
                            }
                        }
                    }
                }, o => Server is LocalApp);
            }
        }
    }
}