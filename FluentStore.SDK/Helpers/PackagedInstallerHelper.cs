﻿using FluentStore.SDK.Images;
using FluentStore.SDK.Messages;
using FluentStore.SDK.Models;
using Microsoft.Marketplace.Storefront.Contracts.Enums;
using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.System;
using Windows.System.Profile;

namespace FluentStore.SDK.Helpers
{
    public static class PackagedInstallerHelper
    {
        /// <inheritdoc cref="PackageBase.GetCannotBeInstalledReason"/>
        public static async Task<string> GetCannotBeInstalledReason(IStorageFile installerFile, bool isBundle)
        {
            Guard.IsNotNull(installerFile, nameof(installerFile));

            // Open package archive for reading
            using var stream = await installerFile.OpenReadAsync();
            using var archive = new ZipArchive(stream.AsStream());

            // Extract metadata from manifest
            List<ProcessorArchitecture> architectures = new List<ProcessorArchitecture>();
            if (isBundle)
            {
                var bundleManifestEntry = archive.GetEntry("AppxManifest\\AppxBundleManifest.xml");
                using var bundleManifestStream = bundleManifestEntry.Open();
                XPathDocument bundleManifest = new XPathDocument(bundleManifestStream);
                var archNodes = bundleManifest.CreateNavigator().Select("//Package/@Architecture");
                do
                {
                    var archNode = archNodes.Current;
                    architectures.Add((ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), archNode.Value, true));
                } while (archNodes.MoveNext());
            }
            else
            {
                var manifestEntry = archive.GetEntry("AppxManifest.xml");
                using var manifestStream = manifestEntry.Open();
                XPathDocument manifest = new XPathDocument(manifestStream);
                var archNode = manifest.CreateNavigator().SelectSingleNode("//Identity/@ProcessorArchitecture");
                architectures.Add((ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), archNode.Value, true));
            }

            // Check Windows platform
            PlatWindows? currentPlat = PlatWindowsStringConverter.Parse(AnalyticsInfo.VersionInfo.DeviceFamily);
            if (!currentPlat.HasValue)
            {
                return "Cannot identify the current Windows platform.";
            }
            //else if (!AllowedPlatforms.Contains(currentPlat.Value))
            //{
            //    return Title + " does not support " + currentPlat.ToString();
            //}

            // Check CPU architecture
            var curArch = Package.Current.Id.Architecture;
            if (!architectures.Contains(curArch))
            {
                return "Package does not support " + curArch.ToString();
            }

            return null;
        }

        private static readonly Uri dummyUri = new Uri("mailto:dummy@uwpcommunity.com");
        public static async Task<bool> IsInstalled(string packageFamilyName)
        {
            bool appInstalled;
            LaunchQuerySupportStatus result = await Launcher.QueryUriSupportAsync(dummyUri, LaunchQuerySupportType.Uri, packageFamilyName);
            switch (result)
            {
                case LaunchQuerySupportStatus.Available:
                case LaunchQuerySupportStatus.NotSupported:
                    appInstalled = true;
                    break;
                //case LaunchQuerySupportStatus.AppNotInstalled:
                //case LaunchQuerySupportStatus.AppUnavailable:
                //case LaunchQuerySupportStatus.Unknown:
                default:
                    appInstalled = false;
                    break;
            }

            return appInstalled;
        }

        /// <inheritdoc cref="PackageBase.InstallAsync"/>
        public static async Task<bool> Install(PackageBase package)
        {
            PackageManager pkgManager = new PackageManager();
            Progress<DeploymentProgress> progressCallback = new Progress<DeploymentProgress>(prog =>
            {
                WeakReferenceMessenger.Default.Send(new PackageInstallProgressMessage(package, prog.percentage / 100));
            });

            WeakReferenceMessenger.Default.Send(new PackageInstallStartedMessage(package));

            // Attempt to install the downloaded package
            // WinRT never sends a progress callback, so don't bother registering one
            var result = await pkgManager.AddPackageByUriAsync(
                new Uri(package.DownloadItem.Path),
                new AddPackageOptions()
                {
                    ForceAppShutdown = true
                }
            );

            if (!result.IsRegistered)
            {
                WeakReferenceMessenger.Default.Send(new PackageInstallFailedMessage(package, new Exception(result.ErrorText)));
                return false;
            }

            // Fire the success callback
            WeakReferenceMessenger.Default.Send(new PackageInstallCompletedMessage(package));

            return true;
        }

        /// <inheritdoc cref="PackageBase.LaunchAsync"/>
        public static async Task<bool> Launch(string packageFamilyName)
        {
            var pkgManager = new PackageManager();
            var pkg = pkgManager.FindPackagesForUser(string.Empty, packageFamilyName).FirstOrDefault();
            if (pkg == null) return false;

            var apps = await pkg.GetAppListEntriesAsync();
            var firstApp = apps.FirstOrDefault();
            if (firstApp == null) return false;

            return await firstApp.LaunchAsync();
        }

        public static async Task<InstallerType> GetInstallerType(StorageFile file)
        {
            InstallerType type = InstallerType.Unknown;

            using (var stream = await file.OpenStreamForReadAsync())
            {
                var bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                uint magicNumber = (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

                switch (magicNumber)
                {
                    // ZIP
                    /// Typical [not empty or spanned] ZIP archive
                    case 0x504B0304:
                        using (var archive = ZipFile.OpenRead(file.Path))
                        {
                            var entry = archive.GetEntry("[Content_Types].xml");
                            var ctypesXml = XDocument.Load(entry.Open());
                            var defaults = ctypesXml.Root.Elements().Where(e => e.Name.LocalName == "Default");
                            if (defaults.Any(d => d.Attribute("Extension").Value == "msix"))
                            {
                                // Package contains one or more MSIX packages
                                type |= InstallerType.Msix;
                            }
                            else if (defaults.Any(d => d.Attribute("Extension").Value == "appx"))
                            {
                                // Package contains one or more APPX packages
                                type |= InstallerType.AppX;
                            }
                            if (defaults.Any(d => d.Attribute("ContentType").Value == "application/vnd.ms-appx.bundlemanifest+xml"))
                            {
                                // Package is a bundle
                                type |= InstallerType.Bundle;
                            }

                            if (type == InstallerType.Unknown)
                            {
                                // We're not sure exactly what kind of package it is, but it's definitely
                                // a package archive. Even if it's not actually an appxbundle, it will
                                // likely still work.
                                type = InstallerType.AppXBundle;
                            }
                        }
                        break;

                    // EMSIX, EAAPX, EMSIXBUNDLE, EAPPXBUNDLE
                    /// An encrypted installer [bundle]?
                    case 0x45584248:
                        // This means the downloaded file wasn't a zip archive.
                        // Some inspection of a hex dump of the file leads me to believe that this means
                        // the installer is encrypted. There's probably nothing that can be done about this,
                        // but since it's a known case, let's leave this here.
                        type = InstallerType.EAppXBundle;
                        break;
                }
            }

            return type;
        }

        /// <inheritdoc cref="PackageBase.GetAppIcon"/>
        public static async Task<ImageBase> GetAppIcon(StorageFile file, bool isBundle)
        {
            // Open package archive for reading
            using var stream = await file.OpenReadAsync();
            using var archive = new ZipArchive(stream.AsStream());

            // Extract icon from manifest
            ZipArchive packArchive;
            if (isBundle)
            {
                // Get the smallest application APPX/MSIX
                var bundleManifestEntry = archive.GetEntry("AppxManifest\\AppxBundleManifest.xml");
                using var bundleManifestStream = bundleManifestEntry.Open();
                XPathDocument bundleManifest = new XPathDocument(bundleManifestStream);
                var packageNodes = bundleManifest.CreateNavigator().Select("/Bundle/Packages/Package[@Type=\"application\"]");
                XPathNavigator smallestPackEntry = null;
                long smallestPackSize = long.MaxValue;
                do
                {
                    var packEntry = packageNodes.Current;
                    long packSize = long.Parse(packEntry.GetAttribute("Size", string.Empty));
                    if (packSize < smallestPackSize)
                        smallestPackEntry = packEntry;
                } while (packageNodes.MoveNext());

                // Open the APPX/MSIX
                using Stream packStream = archive.GetEntry(smallestPackEntry.GetAttribute("FileName", string.Empty)).Open();
                packArchive = new ZipArchive(packStream);
            }
            else
            {
                packArchive = archive;
            }

            // Get the app icon
            var manifestEntry = archive.GetEntry("AppxManifest.xml");
            using var manifestStream = manifestEntry.Open();
            XPathDocument manifest = new XPathDocument(manifestStream);
            var logoNode = manifest.CreateNavigator().Select("/Package/Properties/Logo[1]").Current;
            var iconEntry = archive.GetEntry(logoNode.Value);
            return new StreamImage
            {
                ImageType = Images.ImageType.Logo,
                BackgroundColor = "Transparent",
                Stream = iconEntry.Open()
            };
        }
    }
}
