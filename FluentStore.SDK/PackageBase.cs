﻿using FluentStore.SDK.Images;
using FluentStore.SDK.Models;
using Garfoot.Utilities.FluentUrn;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace FluentStore.SDK
{
    public abstract class PackageBase : ObservableObject, IEquatable<PackageBase>
    {
        /// <summary>
        /// A Uniform Resource Name (URN) that represents this specific package.
        /// </summary>
        /// <remarks>
        /// <see cref="Urn.NamespaceIdentifier"/> is the name of the <see cref="PackageHandlerBase"/>
        /// that handles this package, <see cref="Urn.UnEscapedValue"/> is the handler-specific
        /// ID of this package.
        /// </remarks>
        public abstract Urn Urn { get; set; }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether <see cref="GetCannotBeInstalledReason"/>
        /// and <see cref="CanBeInstalled"/> requires the package to be downloaded first.
        /// </summary>
        public virtual bool RequiresDownloadForCompatCheck => false;

        /// <summary>
        /// Gets a message describing why this package cannot be installed on this system.
        /// </summary>
        /// <returns><c>null</c> if it can be installed, a reason if it can't.</returns>
        public virtual Task<string> GetCannotBeInstalledReason() => null;

        /// <summary>
        /// Determines if this package can be installed on this system.
        /// </summary>
        public bool CanBeInstalled() => GetCannotBeInstalledReason() == null;

        public abstract Task<bool> InstallAsync();

        public abstract Task<IStorageItem> DownloadPackageAsync(StorageFolder folder = null);

        public abstract Task<bool> IsPackageInstalledAsync();

        public abstract Task LaunchAsync();

        public virtual void OnDownloaded(IStorageItem item) { }

        public virtual bool Equals(PackageBase other) => this.Urn.Equals(other.Urn);

        public override string ToString() => Title;

        /// <summary>
        /// The underlying model for this package class.
        /// </summary>
        private object _Model;
        public virtual object Model
        {
            get => _Model;
            set => SetProperty(ref _Model, value);
        }

        private PackageStatus _Status = PackageStatus.Unknown;
        public PackageStatus Status
        {
            get => _Status;
            set => SetProperty(ref _Status, value);
        }

        private IStorageItem _DownloadItem;
        public IStorageItem DownloadItem
        {
            get => _DownloadItem;
            set
            {
                OnDownloaded(value);
                SetProperty(ref _DownloadItem, value);
            }
        }

        private string _Title;
        public string Title
        {
            get => _Title;
            set => SetProperty(ref _Title, value);
        }

        private string _PublisherId;
        public string PublisherId
        {
            get => _PublisherId;
            set => SetProperty(ref _PublisherId, value);
        }

        private string _DeveloperName;
        public string DeveloperName
        {
            get => _DeveloperName;
            set => SetProperty(ref _DeveloperName, value);
        }

        /// <summary>
        /// The date this specific package was released.
        /// </summary>
        private DateTimeOffset _ReleaseDate;
        public DateTimeOffset ReleaseDate
        {
            get => _ReleaseDate;
            set => SetProperty(ref _ReleaseDate, value);
        }

        private string _Description;
        public string Description
        {
            get => _Description;
            set => SetProperty(ref _Description, value);
        }

        private string _Version;
        public string Version
        {
            get => _Version;
            set => SetProperty(ref _Version, value);
        }

        private ReviewSummary _ReviewSummary = default(ReviewSummary);
        public ReviewSummary ReviewSummary
        {
            get => _ReviewSummary;
            set => SetProperty(ref _ReviewSummary, value);
        }
        public bool HasReviewSummary => ReviewSummary != default(ReviewSummary);

        private double _Price = -1;
        public double Price
        {
            get => _Price;
            set => SetProperty(ref _Price, value);
        }
        public bool HasPrice => Price >= 0;

        private string _DisplayPrice;
        public string DisplayPrice
        {
            get => _DisplayPrice;
            set => SetProperty(ref _DisplayPrice, value);
        }
        public bool HasDisplayPrice => DisplayPrice != null;

        private string _ShortTitle;
        public string ShortTitle
        {
            get => _ShortTitle ?? Title;
            set => _ShortTitle = value;
        }

        private string _Website;
        public string Website
        {
            get => _Website;
            set => SetProperty(ref _Website, value);
        }
        public bool HasWebsite => Website != null;

        private List<ImageBase> _Images = new List<ImageBase>();
        public List<ImageBase> Images
        {
            get => _Images;
            set => SetProperty(ref _Images, value);
        }

        /// <summary>
        /// Gets the app's icon.
        /// </summary>
        public abstract Task<ImageBase> GetAppIcon();

        /// <summary>
        /// Gets the app's hero image.
        /// </summary>
        public abstract Task<ImageBase> GetHeroImage();

        /// <summary>
        /// Gets the app's screenshots.
        /// </summary>
        public abstract Task<List<ImageBase>> GetScreenshots();
    }

    public abstract class PackageBase<TModel> : PackageBase
    {
        private TModel _Model;
        public new TModel Model
        {
            get => _Model;
            set => SetProperty(ref _Model, value);
        }
    }

    public enum PackageStatus
    {
        Unknown         = 0xFFFF,
        None            = 0,
        DownloadReady   = 1,
        Downloaded      = 2,
        Installed       = 3
    }
}
