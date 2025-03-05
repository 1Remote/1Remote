using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
        private void AppArgumentsBulkInit(IEnumerable<ProtocolBase> servers)
        {
            var sharedArgumentsInBuckEdit = new List<AppArgument>();
            var protocolBases = servers as ProtocolBase[] ?? servers.ToArray();
            if (Server is LocalApp app && protocolBases.All(x => x is LocalApp))
            {
                bool isAllTheSameFlag = true;
                var ss = protocolBases.Select(x => (LocalApp)x).ToArray();
                if (ss.Any(x => x.ArgumentList.Count != ss.First().ArgumentList.Count))
                {
                    isAllTheSameFlag = false;
                }

                if (isAllTheSameFlag)
                {
                    for (int i = 0; i < ss.First().ArgumentList.Count; i++)
                    {
                        if (ss.All(x => x.ArgumentList[i].IsConfigEqualTo(ss.First().ArgumentList[i])))
                        {
                            var newArg = (AppArgument)ss.First().ArgumentList[i].Clone();
                            if (ss.Any(x=> x.ArgumentList[i].Value != newArg.Value))
                            {
                                if (newArg.Type == AppArgumentType.Selection && !newArg.Selections.ContainsKey(Server.ServerEditorDifferentOptions))
                                {
                                    newArg.Selections.Add(Server.ServerEditorDifferentOptions, Server.ServerEditorDifferentOptions);
                                }
                                newArg.Value = Server.ServerEditorDifferentOptions;
                            }
                            sharedArgumentsInBuckEdit.Add(newArg);
                        }
                        else
                        {
                            isAllTheSameFlag = false;
                            break;
                        }
                    }
                }

                var list = new List<AppArgument>();
                if (isAllTheSameFlag == false)
                    list.Add(new AppArgument(false) { Name = Server.ServerEditorDifferentOptions, Value = Server.ServerEditorDifferentOptions, Type = AppArgumentType.Const});
                else
                    list.AddRange(sharedArgumentsInBuckEdit);
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
                    var argumentList = new List<AppArgument>();
                    foreach (var appArgument in newServer.ArgumentList)
                    {
                        if (appArgument.Name == Server.ServerEditorDifferentOptions)
                        {
                            argumentList.AddRange(protocolBeforeEdit.ArgumentList.Select(x => (AppArgument)x.Clone()));
                        }
                        else
                        {
                            var arg = (AppArgument)appArgument.Clone();
                            arg.Value = appArgument.Value;
                            if (arg.Value == Server.ServerEditorDifferentOptions 
                                && protocolBeforeEdit.ArgumentList.FirstOrDefault(x=>x.Name == arg.Name) is { } argOld)
                            {
                                arg.Value = argOld.Value;
                            }
                            if (arg.Type == AppArgumentType.Selection && arg.Selections.ContainsKey(Server.ServerEditorDifferentOptions))
                            {
                                arg.Selections.Remove(Server.ServerEditorDifferentOptions);
                            }
                            argumentList.Add(arg);
                        }
                    }
                    protocolBeforeEdit.ArgumentList = new ObservableCollection<AppArgument>(argumentList);
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
                        var arguments = protocol.ArgumentList.Select(x => x.Name).ToList();
                        if (IsBuckEdit && _serversInBuckEdit?.Count() > 0)
                        {
                            foreach (var s in _serversInBuckEdit)
                            {
                                if (s is LocalApp p)
                                    arguments.AddRange(p.ArgumentList.Select(x => x.Name));
                            }
                        }
                        arguments = arguments.Distinct().ToList();
                        var vm = new ArgumentEditViewModel(protocol, arguments, o as AppArgument);
                        MaskLayerController.ShowWindowWithMask(vm);
                        Task.Factory.StartNew(async void () =>
                        {
                            await vm.WaitDialogResult();
                        });
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