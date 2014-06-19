﻿using Jojatekok.MoneroAPI;
using Ookii.Dialogs.Wpf;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Jojatekok.MoneroGUI.Windows
{
    public partial class MainWindow : IDisposable
    {
        private bool IsDisposeInProgress { get; set; }

        private static MoneroClient MoneroClient { get; set; }
        private static Logger LoggerDaemon { get; set; }
        private static Logger LoggerWallet { get; set; }

        public static readonly RoutedCommand BackupWalletCommand = new RoutedCommand();
        public static readonly RoutedCommand ExportCommand = new RoutedCommand();
        public static readonly RoutedCommand ExitCommand = new RoutedCommand();
        public static readonly RoutedCommand OptionsCommand = new RoutedCommand();
        public static readonly RoutedCommand ShowDebugWindowCommand = new RoutedCommand();
        public static readonly RoutedCommand ShowAboutWindowCommand = new RoutedCommand();

        private DebugWindow DebugWindow { get; set; }

        public MainWindow()
        {
            Icon = StaticObjects.ApplicationIcon;

            InitializeComponent();

            MoneroClient = StaticObjects.MoneroClient;
            LoggerDaemon = StaticObjects.LoggerDaemon;
            LoggerWallet = StaticObjects.LoggerWallet;

            MoneroClient.Daemon.OnLogMessage += Daemon_OnLogMessage;
            MoneroClient.Daemon.NetworkInformationChanging += Daemon_NetworkInformationChanging;
            MoneroClient.Daemon.BlockchainSynced += Daemon_BlockchainSynced;

            MoneroClient.Wallet.OnLogMessage += Wallet_OnLogMessage;
            MoneroClient.Wallet.AddressReceived += Wallet_AddressReceived;
            MoneroClient.Wallet.BalanceChanging += Wallet_BalanceChanging;

            OverviewView.ViewModel.TransactionDataSource = MoneroClient.Wallet.Transactions;
            TransactionsView.ViewModel.DataSource = MoneroClient.Wallet.Transactions;

            MoneroClient.Start();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!IsDisposeInProgress) {
                Task.Factory.StartNew(Dispose);
                BusyIndicator.IsBusy = true;
            }

            e.Cancel = true;
        }

        private void BackupWalletCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog { RootFolder = Environment.SpecialFolder.MyComputer };
            
            if (dialog.ShowDialog() == true) {
                MoneroClient.Wallet.BackupAsync(dialog.SelectedPath);
            }
        }

        private void ExportCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.Assert(TabControl.SelectedContent as IExportable != null, "TabControl.SelectedContent as IExportable != null");
            (TabControl.SelectedContent as IExportable).Export();
        }

        private void ExitCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void OptionsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            new OptionsWindow(this).ShowDialog();
        }

        private void ShowDebugWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DebugWindow == null) {
                DebugWindow = new DebugWindow();
                DebugWindow.Closed += delegate { DebugWindow = null; };
                DebugWindow.Show();

            } else {
                DebugWindow.Activate();
            }
        }

        private void ShowAboutWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            new AboutWindow(this).ShowDialog();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = e.AddedItems[0] as TabItem;
            if (selectedItem == null) return;

            if (selectedItem.Content is IExportable) {
                MenuItemExport.IsEnabled = true;
            } else {
                MenuItemExport.IsEnabled = false;
            }
        }

        private static void Daemon_OnLogMessage(object sender, string e)
        {
            LoggerDaemon.Log(e);
        }

        private void Daemon_NetworkInformationChanging(object sender, NetworkInformationChangingEventArgs e)
        {
            var newValue = e.NewValue;
            var statusBarViewModel = StatusBar.ViewModel;

            statusBarViewModel.ConnectionCount = (byte)(newValue.ConnectionCountIncoming + newValue.ConnectionCountOutgoing);

            statusBarViewModel.BlocksTotal = newValue.BlockHeightTotal;
            statusBarViewModel.BlocksDownloaded = newValue.BlockHeightDownloaded;

            statusBarViewModel.SyncBarText = string.Format(Helper.InvariantCulture,
                                                           Properties.Resources.StatusBarSyncTextMain,
                                                           newValue.BlockHeightRemaining,
                                                           newValue.BlockTimeRemaining.ToStringReadable());
            statusBarViewModel.SyncBarVisibility = Visibility.Visible;
        }

        private void Daemon_BlockchainSynced(object sender, EventArgs e)
        {
            StatusBar.ViewModel.SyncBarVisibility = Visibility.Hidden;
        }

        private static void Wallet_OnLogMessage(object sender, string e)
        {
            LoggerWallet.Log(e);
        }

        private void Wallet_AddressReceived(object sender, string e)
        {
            OverviewView.ViewModel.Address = e;
        }

        private void Wallet_BalanceChanging(object sender, BalanceChangingEventArgs e)
        {
            var newValue = e.NewValue;

            var overviewViewModel = OverviewView.ViewModel;
            overviewViewModel.BalanceSpendable = newValue.Spendable;
            overviewViewModel.BalanceUnconfirmed = newValue.Unconfirmed;

            var sendCoinsViewModel = SendCoinsView.ViewModel;
            sendCoinsViewModel.CoinBalance = newValue.Spendable;
            sendCoinsViewModel.IsSendingEnabled = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !IsDisposeInProgress) {
                IsDisposeInProgress = true;
                
                if (MoneroClient != null) {
                    MoneroClient.Dispose();
                }

                Dispatcher.Invoke(Application.Current.Shutdown);
            }
        }
    }
}