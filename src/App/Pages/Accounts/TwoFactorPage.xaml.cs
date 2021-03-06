﻿using Bit.App.Controls;
using Bit.App.Models;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class TwoFactorPage : BaseContentPage
    {
        private readonly IBroadcasterService _broadcasterService;
        private readonly IMessagingService _messagingService;
        private readonly IStorageService _storageService;
        private readonly AppOptions _appOptions;

        private TwoFactorPageViewModel _vm;
        private bool _inited;

        public TwoFactorPage(AppOptions appOptions = null)
        {
            InitializeComponent();
            SetActivityIndicator();
            _appOptions = appOptions;
            _storageService = ServiceContainer.Resolve<IStorageService>("storageService");
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _vm = BindingContext as TwoFactorPageViewModel;
            _vm.Page = this;
            _vm.TwoFactorAction = () => Device.BeginInvokeOnMainThread(async () => await TwoFactorAuthAsync());
            DuoWebView = _duoWebView;
            if (Device.RuntimePlatform == Device.Android)
            {
                ToolbarItems.Remove(_cancelItem);
            }
        }

        public HybridWebView DuoWebView { get; set; }

        public void AddContinueButton()
        {
            if (!ToolbarItems.Contains(_continueItem))
            {
                ToolbarItems.Add(_continueItem);
            }
        }

        public void RemoveContinueButton()
        {
            if (ToolbarItems.Contains(_continueItem))
            {
                ToolbarItems.Remove(_continueItem);
            }
        }

        protected async override void OnAppearing()
        {
            base.OnAppearing();
            _broadcasterService.Subscribe(nameof(TwoFactorPage), (message) =>
            {
                if (message.Command == "gotYubiKeyOTP")
                {
                    var token = (string)message.Data;
                    if (_vm.YubikeyMethod && !string.IsNullOrWhiteSpace(token) &&
                        token.Length == 44 && !token.Contains(" "))
                    {
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            _vm.Token = token;
                            await _vm.SubmitAsync();
                        });
                    }
                }
                else if (message.Command == "resumeYubiKey")
                {
                    if (_vm.YubikeyMethod)
                    {
                        _messagingService.Send("listenYubiKeyOTP", true);
                    }
                }
            });

            if (!_inited)
            {
                _inited = true;
                await LoadOnAppearedAsync(_scrollView, true, () =>
                {
                    _vm.Init();
                    if (_vm.TotpMethod)
                    {
                        RequestFocus(_totpEntry);
                    }
                    return Task.FromResult(0);
                });
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (!_vm.YubikeyMethod)
            {
                _messagingService.Send("listenYubiKeyOTP", false);
                _broadcasterService.Unsubscribe(nameof(TwoFactorPage));
            }
        }
        protected override bool OnBackButtonPressed()
        {
            if (_vm.YubikeyMethod)
            {
                _messagingService.Send("listenYubiKeyOTP", false);
                _broadcasterService.Unsubscribe(nameof(TwoFactorPage));
            }
            return base.OnBackButtonPressed();
        }

        private async void Continue_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.SubmitAsync();
            }
        }

        private async void Methods_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.AnotherMethodAsync();
            }
        }

        private async void ResendEmail_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.SendEmailAsync(true, true);
            }
        }

        private async void Close_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await Navigation.PopModalAsync();
            }
        }

        private void TryAgain_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                if (_vm.YubikeyMethod)
                {
                    _messagingService.Send("listenYubiKeyOTP", true);
                }
            }
        }
        
        private async Task TwoFactorAuthAsync()
        {
            if (_appOptions != null)
            {
                if (_appOptions.FromAutofillFramework && _appOptions.SaveType.HasValue)
                {
                    Application.Current.MainPage = new NavigationPage(new AddEditPage(appOptions: _appOptions));
                    return;
                }
                if (_appOptions.Uri != null)
                {
                    Application.Current.MainPage = new NavigationPage(new AutofillCiphersPage(_appOptions));
                    return;
                }
            }
            var previousPage = await _storageService.GetAsync<PreviousPageInfo>(Constants.PreviousPageKey);
            if (previousPage != null)
            {
                await _storageService.RemoveAsync(Constants.PreviousPageKey);
            }
            Application.Current.MainPage = new TabsPage(_appOptions, previousPage);
        }
    }
}
