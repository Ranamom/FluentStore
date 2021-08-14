﻿using FluentStore.SDK;
using FluentStore.SDK.Attributes;
using FluentStore.Services;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FluentStore.ViewModels
{
    public class PackageViewModel : ObservableObject
    {
        public PackageViewModel()
        {
            ViewProductCommand = new RelayCommand<object>(ViewPackage);
        }
        public PackageViewModel(PackageBase package) : this()
        {
            Package = package;
        }

        private readonly INavigationService NavigationService = Ioc.Default.GetRequiredService<INavigationService>();

        private PackageBase _Package;
        public PackageBase Package
        {
            get => _Package;
            set
            {
                SetProperty(ref _Package, value);

                // Reset cached properties
                AppIcon = null;
                HeroImage = null;
                Screenshots = null;
                DisplayProperties = null;
                DisplayAdditionalInformationProperties = null;

                // Update derived properties
                IsCollection = Package.GetType() == typeof(SDK.PackageTypes.CollectionPackage);
            }
        }

        private IRelayCommand<object> _ViewProductCommand;
        public IRelayCommand<object> ViewProductCommand
        {
            get => _ViewProductCommand;
            set => SetProperty(ref _ViewProductCommand, value);
        }

        private IAsyncRelayCommand<object> _DownloadCommand;
        public IAsyncRelayCommand<object> DownloadCommand
        {
            get => _DownloadCommand;
            set => SetProperty(ref _DownloadCommand, value);
        }

        private IAsyncRelayCommand<object> _InstallCommand;
        public IAsyncRelayCommand<object> InstallCommand
        {
            get => _InstallCommand;
            set => SetProperty(ref _InstallCommand, value);
        }

        private IAsyncRelayCommand<object> _SaveToCollectionCommand;
        public IAsyncRelayCommand<object> SaveToCollectionCommand
        {
            get => _SaveToCollectionCommand;
            set => SetProperty(ref _SaveToCollectionCommand, value);
        }

        private bool _IsCollection;
        public bool IsCollection
        {
            get => _IsCollection;
            private set => SetProperty(ref _IsCollection, value);
        }

        private ImageBase _AppIcon;
        public ImageBase AppIcon
        {
            get
            {
                if (_AppIcon == null && Package != null)
                {
                    // Yes, this will block the UI thread. Hopefully it's not for too long.
                    AppIcon = Package.GetAppIcon()?.Result;
                }

                return _AppIcon;
            }
            set => SetProperty(ref _AppIcon, value);
        }

        private ImageBase _HeroImage;
        public ImageBase HeroImage
        {
            get
            {
                if (_HeroImage == null)
                {
                    // Yes, this will block the UI thread. Hopefully it's not for too long.
                    HeroImage = Package.GetHeroImage()?.Result;
                }

                return _HeroImage;
            }
            set => SetProperty(ref _HeroImage, value);
        }

        private List<ImageBase> _Screenshots;
        public List<ImageBase> Screenshots
        {
            get
            {
                if (_Screenshots == null && Package != null)
                {
                    // Yes, this will block the UI thread. Hopefully it's not for too long.
                    Screenshots = Package.GetScreenshots()?.Result;
                }

                return _Screenshots;
            }
            set => SetProperty(ref _Screenshots, value);
        }

        public string AverageRatingString => Package.HasAverageRating
            ? Package.AverageRating.ToString("F1")
            : string.Empty;

        public void ViewPackage(object obj)
        {
            PackageViewModel pvm;
            switch (obj)
            {
                case PackageViewModel viewModel:
                    pvm = viewModel;
                    break;
                case PackageBase package:
                    pvm = new PackageViewModel(package);
                    break;
                default:
                    throw new ArgumentException($"'{nameof(obj)}' is an invalid type: {obj.GetType().Name}");
            }
            NavigationService.Navigate(pvm);
        }

        private List<DisplayInfo> _DisplayProperties;
        /// <summary>
        /// Gets the value of all properties with <see cref="DisplayAttribute"/> applied.
        /// </summary>
        public List<DisplayInfo> DisplayProperties
        {
            get
            {
                if (_DisplayProperties == null)
                {
                    _DisplayProperties = new List<DisplayInfo>();
                    Type type = Package.GetType();
                    foreach (PropertyInfo prop in type.GetProperties())
                    {
                        var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
                        // Filter out properties without the attribute, and ignore DisplayAdditionalInformationInfo
                        if (displayAttr == null || displayAttr.GetType() != typeof(DisplayAttribute))
                            continue;

                        object value = prop.GetValue(Package);
                        if (value == null)
                            continue;
                        _DisplayProperties.Add(new DisplayInfo(displayAttr, value));
                    }
                }
                return _DisplayProperties;
            }
            set => SetProperty(ref _DisplayProperties, value);
        }


        private List<DisplayAdditionalInformationInfo> _DisplayAdditionalInformationProperties;
        /// <summary>
        /// Gets the value of all properties with <see cref="DisplayAdditionalInformationAttribute"/> applied.
        /// </summary>
        public List<DisplayAdditionalInformationInfo> DisplayAdditionalInformationProperties
        {
            get
            {
                if (_DisplayAdditionalInformationProperties == null)
                {
                    _DisplayAdditionalInformationProperties = new List<DisplayAdditionalInformationInfo>();
                    Type type = Package.GetType();
                    foreach (PropertyInfo prop in type.GetProperties())
                    {
                        var displayAttr = prop.GetCustomAttribute<DisplayAdditionalInformationAttribute>();
                        // Filter out properties without the attribute
                        if (displayAttr == null)
                            continue;

                        object value = prop.GetValue(Package);
                        if (value == null)
                            continue;
                        _DisplayAdditionalInformationProperties.Add(new DisplayAdditionalInformationInfo(displayAttr, value));
                    }
                }
                return _DisplayAdditionalInformationProperties;
            }
            set => SetProperty(ref _DisplayAdditionalInformationProperties, value);
        }

        public static implicit operator PackageBase(PackageViewModel pvm) => pvm.Package;
        public static implicit operator PackageViewModel(PackageBase pb) => new PackageViewModel(pb);
    }
}
