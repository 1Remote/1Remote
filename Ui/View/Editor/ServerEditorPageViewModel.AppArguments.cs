using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using _1RM.View.Editor.Forms;
using _1RM.View.Editor.Forms.Argument;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace _1RM.View.Editor
{
    public partial class ServerEditorPageViewModel : NotifyPropertyChangedBase
    {
        private readonly List<AppArgument> _sharedArgumentsInBuckEdit = new List<AppArgument>();
        private void AppArgumentsBulkInit(IEnumerable<ProtocolBase> servers)
        {
            // TODO need a remake，批量编辑时，参数列表不同，如何批量修改列表、更改参数顺序、删除参数、新增参数？
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
                        if (ss.All(x => x.ArgumentList.Any(y => y.IsConfigEqualTo(argument) == true)))
                        {
                            if (_sharedArgumentsInBuckEdit.All(x => x.IsConfigEqualTo(argument) == false))
                            {
                                var newArg = (AppArgument)argument.Clone();
                                var vv = ss.Any(x => x.ArgumentList.Where(y => y.IsConfigEqualTo(argument)).Any(z => z.Value != argument.Value));
                                if (vv)
                                {
                                    newArg.Value = Server.ServerEditorDifferentOptions;
                                }
                                _sharedArgumentsInBuckEdit.Add(newArg);
                            }
                        }
                        else
                        {
                            isAllTheSameFlag = false;
                        }
                    }
                }

                var list = new List<AppArgument>();
                if (isAllTheSameFlag == false)
                    list.Add(new AppArgument(false) { Name = Server.ServerEditorDifferentOptions, Value = Server.ServerEditorDifferentOptions, Type = AppArgumentType.Const});
                //else
                    list.AddRange(_sharedArgumentsInBuckEdit);

                app.ArgumentList = new ObservableCollection<AppArgument>(list);
            }
        }
        private void AppArgumentsBulkMerge(IEnumerable<ProtocolBase> servers)
        {
            var protocolBases = servers as ProtocolBase[] ?? servers.ToArray();
            if (Server is LocalApp newServer && protocolBases.All(x => x is LocalApp))
            {
                var ss = protocolBases.Select(x => (LocalApp)x).ToArray();
                foreach (var protocolBeforeEdit in ss)
                {
                    foreach (var argument in protocolBeforeEdit.ArgumentList.ToArray())
                    {
                        if (_sharedArgumentsInBuckEdit.Any(x => x.IsConfigEqualTo(argument))) // 编辑之前共有，编辑后不再共有，删除，TODO 无法处理改名的情况
                        {
                            if (newServer.ArgumentList.All(x => x.IsConfigEqualTo(argument) == false))
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
                        if (protocolBeforeEdit.ArgumentList.All(x => x.IsConfigEqualTo(credential) == false))
                        {
                            protocolBeforeEdit.ArgumentList.Add(credential);
                        }
                    }
                }
            }
        }




        private bool _isEditMode = false;
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetAndNotifyIfChanged(ref _isEditMode, value);
        }



        private RelayCommand? _cmdToggleEditMode;
        public RelayCommand CmdToggleEditMode
        {
            get
            {
                return _cmdToggleEditMode ??= new RelayCommand((o) =>
                {
                    IsEditMode = !IsEditMode;
                }, o => Server is LocalApp);
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
                        var vm = new ArgumentEditViewModel(protocol, arguments, o as AppArgument);
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
                    if (Server is LocalApp protocol
                        && o is AppArgument org
                        && protocol.ArgumentList.Contains(org))
                    {
                        if (MessageBoxHelper.Confirm(IoC.Translate("confirm_to_delete_selected"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                        {
                            protocol.ArgumentList.Remove(org);
                        }
                    }
                }, o => Server is LocalApp);
            }
        }



        private RelayCommand? _cmdMoveArgumentUp;
        public RelayCommand CmdMoveArgumentUp
        {
            get
            {
                return _cmdMoveArgumentUp ??= new RelayCommand((o) =>
                {
                    if (Server is LocalApp protocol
                        && o is AppArgument org
                        && protocol.ArgumentList.Contains(org))
                    {
                        var index = protocol.ArgumentList.IndexOf(org);
                        if (index > 0)
                        {
                            protocol.ArgumentList.Move(index, index - 1);
                        }
                    }
                }, o => Server is LocalApp);
            }
        }

        private RelayCommand? _cmdMoveArgumentDown;
        public RelayCommand CmdMoveArgumentDown
        {
            get
            {
                return _cmdMoveArgumentDown ??= new RelayCommand((o) =>
                {
                    if (Server is LocalApp protocol
                        && o is AppArgument org
                        && protocol.ArgumentList.Contains(org))
                    {
                        var index = protocol.ArgumentList.IndexOf(org);
                        if (index < protocol.ArgumentList.Count - 1)
                        {
                            protocol.ArgumentList.Move(index, index + 1);
                        }
                    }
                }, o => Server is LocalApp);
            }
        }
    }
}