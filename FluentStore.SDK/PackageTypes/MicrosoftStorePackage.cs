﻿using FluentStore.SDK.Attributes;
using FluentStore.SDK.Images;
using FluentStore.SDK.Messages;
using Garfoot.Utilities.FluentUrn;
using Microsoft.Marketplace.Storefront.Contracts.Enums;
using Microsoft.Marketplace.Storefront.Contracts.V2;
using Microsoft.Marketplace.Storefront.Contracts.V3;
using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using StoreLib.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.System.Profile;

namespace FluentStore.SDK.Packages
{
    public class MicrosoftStorePackage : ModernPackage<ProductDetails>
    {
        public MicrosoftStorePackage(ProductDetails product) => Update(product);

        public void Update(ProductDetails product)
        {
            Guard.IsNotNull(product, nameof(product));
            Model = product;

            // Set base properties
            Title = product.Title;
            PublisherId = product.PublisherId;
            DeveloperName = product.PublisherName;  // TODO: This should be the developer name, but the MS Store has an empty string
            ReleaseDate = product.LastUpdateDateUtc;
            Description = product.Description;
            Version = product.Version;
            AverageRating = product.AverageRating;
            RatingCount = product.RatingCount;
            Price = product.Price;
            DisplayPrice = product.DisplayPrice;
            ShortTitle = product.ShortTitle;
            Website = product.AppWebsiteUrl;
            StoreId = product.ProductId;

            // Set modern package properties
            PackageFamilyName = product.PackageFamilyNames?[0];
            PublisherDisplayName = product.PublisherName;

            // Set MS Store package properties
            PackageId = product.PackageFamilyNames?[0] ?? StoreId;  // Unpackaged apps don't have PackageFamilyName
            Notes = product.Notes;
            Features = product.Features;
            Categories = product.Categories;
            PrivacyUrl = product.PrivacyUrl;
            Platforms = product.Platforms;
            if (product.SupportUris != null)
                foreach (SupportUri uri in product.SupportUris)
                    SupportUrls.Add(uri.Url);
            Ratings = product.ProductRatings;
            PermissionsRequested = product.PermissionsRequested;
            PackageAndDeviceCapabilities = product.PackageAndDeviceCapabilities;
            AllowedPlatforms = product.AllowedPlatforms;
            WarningMessages = product.WarningMessages;
            if (product.Images != null)
                foreach (ImageItem img in product.Images)
                    Images.Add(new MicrosoftStoreImage(img));
        }

        public void Update(PackageInstance packageInstance)
        {
            Guard.IsNotNull(packageInstance, nameof(packageInstance));

            Version = packageInstance.Version.ToString();
            PackageMoniker = packageInstance.PackageMoniker;
            PackageUri = packageInstance.PackageUri;
        }

        private Urn _Urn;
        public override Urn Urn
        {
            get
            {
                if (_Urn == null)
                    _Urn = Urn.Parse("urn:" + Handlers.MicrosoftStoreHandler.NAMESPACE_MSSTORE + ":" + StoreId);
                return _Urn;
            }
            set => _Urn = value;
        }

        public override bool RequiresDownloadForCompatCheck => false;
        public override async Task<string> GetCannotBeInstalledReason()
        {
            // Check Windows platform
            PlatWindows? currentPlat = PlatWindowsStringConverter.Parse(AnalyticsInfo.VersionInfo.DeviceFamily);
            if (!currentPlat.HasValue)
            {
                return "Cannot identify the current Windows platform.";
            }
            else if (AllowedPlatforms != null && !AllowedPlatforms.Contains(currentPlat.Value))
            {
                return Title + " does not support " + currentPlat.ToString();
            }

            // TODO: Check architecture, etc.

            return null;
        }

        public override async Task<bool> DownloadPackageAsync(string installerPath)
        {
            WeakReferenceMessenger.Default.Send(new PackageFetchStartedMessage(this));
            // Find the package URI
            if (!await PopulatePackageUri())
            {
                WeakReferenceMessenger.Default.Send(new PackageFetchFailedMessage(this, new Exception("An unknown error occurred.")));
                return false;
            }
            WeakReferenceMessenger.Default.Send(new PackageFetchCompletedMessage(this));

            // Download the file to the app's temp directory
            if (installerPath == null)
                installerPath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, PackageMoniker);
            var destinationFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(installerPath));
            string fileName = Path.GetFileName(installerPath);
            InstallerFile = await destinationFolder.CreateFileAsync(
                fileName, CreationCollisionOption.ReplaceExisting);

            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation download = downloader.CreateDownload(PackageUri, InstallerFile);
            download.RangesDownloaded += (op, args) =>
            {
                WeakReferenceMessenger.Default.Send(
                    new PackageDownloadProgressMessage(this, op.Progress.BytesReceived, op.Progress.TotalBytesToReceive));
            };

            // Start download
            WeakReferenceMessenger.Default.Send(new PackageDownloadStartedMessage(this));
            await download.StartAsync();

            // Verify success code
            uint statusCode = download.GetResponseInformation().StatusCode;
            if (statusCode < 200 || statusCode >= 300)
            {
                WeakReferenceMessenger.Default.Send(new PackageDownloadFailedMessage(this,
                    new Exception($"Status code {statusCode} did not indicate success.")));
                return false;
            }
            Status = PackageStatus.Downloaded;

            // Set the proper file type and extension
            string extension = await GetInstallerType();
            if (extension != string.Empty)
                await InstallerFile.RenameAsync(InstallerFile.Name + extension, NameCollisionOption.ReplaceExisting);

            WeakReferenceMessenger.Default.Send(new PackageDownloadCompletedMessage(this, InstallerFile));
            return true;
        }

        private async Task<bool> PopulatePackageUri()
        {
            PlatWindows? currentPlat = PlatWindowsStringConverter.Parse(AnalyticsInfo.VersionInfo.DeviceFamily);
            var culture = System.Globalization.CultureInfo.CurrentUICulture;

            var dcathandler = new StoreLib.Services.DisplayCatalogHandler(DCatEndpoint.Production, new StoreLib.Services.Locale(culture, true));
            await dcathandler.QueryDCATAsync(StoreId);
            IEnumerable<PackageInstance> packs = await dcathandler.GetMainPackagesForProductAsync();

            if (currentPlat.HasValue && currentPlat.Value != PlatWindows.Xbox)
                packs = packs.Where(p => p.Version.Revision != 70);
            List<PackageInstance> installables = packs.OrderByDescending(p => p.Version).ToList();
            if (installables.Count < 1)
                return false;
            // TODO: Add addtional checks that might take longer that the user can enable 
            // if they are having issues

            Update(installables.First());
            Status = PackageStatus.DownloadReady;
            return true;
        }

        public override async Task<ImageBase> GetAppIcon()
        {
            // Prefer tiles, then logos, then posters.
            var icons = Images.FindAll(i => i.ImageType == ImageType.Tile);
            if (icons.Count > 0)
                goto done;

            icons = Images.FindAll(i => i.ImageType == ImageType.Logo);
            if (icons.Count > 0)
                goto done;

            icons = Images.FindAll(i => i.ImageType == ImageType.Poster);
            if (icons.Count > 0)
                goto done;

            Guard.IsNotEmpty(icons, nameof(icons));

        done:
            return icons.OrderByDescending(i => i.Width).First();
        }

        public override async Task<ImageBase> GetHeroImage()
        {
            ImageBase img = null;
            int width = 0;
            foreach (ImageBase image in Images.FindAll(i => i.ImageType == ImageType.Hero))
            {
                if (image.Width > width)
                    img = image;
            }

            return img;
        }

        public override async Task<List<ImageBase>> GetScreenshots()
        {
            string deviceFam = AnalyticsInfo.VersionInfo.DeviceFamily.Substring("Windows.".Length);
            var screenshots = Images.Cast<MicrosoftStoreImage>().Where(i => i.ImageType == ImageType.Screenshot
                && !string.IsNullOrEmpty(i.ImagePositionInfo) && i.ImagePositionInfo.StartsWith(deviceFam));

            var sorted = new List<ImageBase>(screenshots.Count());
            foreach (var screenshot in screenshots)
            {
                // length + 1, to skip device family name and the "/"
                string posStr = screenshot.ImagePositionInfo.Substring(deviceFam.Length + 1);
                int pos = int.Parse(posStr);
                if (pos >= sorted.Count)
                    sorted.Add(screenshot);
                else
                    sorted.Insert(pos, screenshot);
            }

            return sorted;
        }

        private List<string> _Notes = new List<string>();
        [Display]
        public List<string> Notes
        {
            get => _Notes;
            set => SetProperty(ref _Notes, value);
        }

        private List<string> _Features = new List<string>();
        [Display]
        public List<string> Features
        {
            get => _Features;
            set => SetProperty(ref _Features, value);
        }

        private List<string> _Categories = new List<string>();
        [Display]
        public List<string> Categories
        {
            get => _Categories;
            set => SetProperty(ref _Categories, value);
        }

        private string _PrivacyUrl;
        [DisplayAdditionalInformation("Privacy url", "\uE71B")]
        public string PrivacyUrl
        {
            get => _PrivacyUrl;
            set => SetProperty(ref _PrivacyUrl, value);
        }

        private List<string> _Platforms = new List<string>();
        public List<string> Platforms
        {
            get => _Platforms;
            set => SetProperty(ref _Platforms, value);
        }

        private List<string> _SupportUrls = new List<string>();
        public List<string> SupportUrls
        {
            get => _SupportUrls;
            set => SetProperty(ref _SupportUrls, value);
        }

        private List<ProductRating> _Ratings = new List<ProductRating>();
        [Display]
        public List<ProductRating> Ratings
        {
            get => _Ratings;
            set => SetProperty(ref _Ratings, value);
        }

        private List<string> _PermissionsRequested = new List<string>();
        [DisplayAdditionalInformation("Permissions", "\uE8D7")]
        public List<string> PermissionsRequested
        {
            get => _PermissionsRequested;
            set => SetProperty(ref _PermissionsRequested, value);
        }

        private List<string> _PackageAndDeviceCapabilities = new List<string>();
        public List<string> PackageAndDeviceCapabilities
        {
            get => _PackageAndDeviceCapabilities;
            set => SetProperty(ref _PackageAndDeviceCapabilities, value);
        }

        private List<PlatWindows> _AllowedPlatforms = new List<PlatWindows>();
        public List<PlatWindows> AllowedPlatforms
        {
            get => _AllowedPlatforms;
            set => SetProperty(ref _AllowedPlatforms, value);
        }

        private List<WarningMessage> _WarningMessages = new List<WarningMessage>();
        public List<WarningMessage> WarningMessages
        {
            get => _WarningMessages;
            set => SetProperty(ref _WarningMessages, value);
        }

        private Uri _PackageUri;
        public Uri PackageUri
        {
            get => _PackageUri;
            set => SetProperty(ref _PackageUri, value);
        }

        private string _PackageMoniker;
        public string PackageMoniker
        {
            get => _PackageMoniker;
            set => SetProperty(ref _PackageMoniker, value);
        }

        private string _StoreId;
        public string StoreId
        {
            get => _StoreId;
            set => SetProperty(ref _StoreId, value);
        }

        private string _PackageId;
        /// <summary>
        /// The PackageFamilyName for packaged apps, <see cref="StoreId"/> for unpackaged apps.
        /// </summary>
        public string PackageId
        {
            get => _PackageId;
            set => SetProperty(ref _PackageId, value);
        }
    }
}