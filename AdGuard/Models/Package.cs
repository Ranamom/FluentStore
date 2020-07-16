﻿using System;

namespace AdGuard.Models
{
    public class Package
    {
        public string Name { get; set; }

        public string Url { get; set; }

        public DateTimeOffset ExpiryDate { get; set; }

        public Uri Uri
        {
            get
            {
                try
                {
                    return new Uri(Url);
                }
                catch
                {
                    return null;
                }
            }
        }

        public string PackageFamily
        {
            get
            {
                try
                {
                    return Name.Split('_')[0];
                }
                catch { return null; }
            }
        }

        public Version Version
        {
            get
            {
                try
                {
                    return new Version(Name.Split('_')[1]);
                }
                catch { return null; }
            }
        }

        public string Architecture
        {
            get
            {
                try
                {
                    return Name.Split('_')[2];
                }
                catch { return null; }
            }
        }

        public string PublisherId
        {
            get
            {
                try
                {
                    return Name.Split('_')[4];
                }
                catch { return null; }
            }
        }
    }
}
