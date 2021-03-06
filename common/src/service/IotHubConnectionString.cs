﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;

namespace Microsoft.Azure.Devices
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Common.Security;

    sealed class IotHubConnectionString : IAuthorizationHeaderProvider, ICbsTokenProvider
    {
        static readonly TimeSpan DefaultTokenTimeToLive = TimeSpan.FromHours(1);
        const string UserSeparator = "@";

        public IotHubConnectionString(IotHubConnectionStringBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }

            this.Audience = builder.HostName;
            this.HostName = string.IsNullOrEmpty(builder.GatewayHostName) ? builder.HostName : builder.GatewayHostName;
            this.SharedAccessKeyName = builder.SharedAccessKeyName;
            this.SharedAccessKey = builder.SharedAccessKey;
            this.SharedAccessSignature = builder.SharedAccessSignature;
            this.IotHubName = builder.IotHubName;
            this.HttpsEndpoint = new UriBuilder("https", this.HostName).Uri;
            this.AmqpEndpoint = new UriBuilder(CommonConstants.AmqpsScheme, builder.HostName, AmqpConstants.DefaultSecurePort).Uri;
            this.DeviceId = builder.DeviceId;
            this.ModuleId = builder.ModuleId;
            this.GatewayHostName = builder.GatewayHostName;
        }

        public string IotHubName
        {
            get;
            private set;
        }

        public string HostName
        {
            get;
            private set;
        }

        public Uri HttpsEndpoint
        {
            get;
            private set;
        }

        public Uri AmqpEndpoint
        {
            get;
            private set;
        }

        public string Audience
        {
            get;
            private set;
        }

        public string SharedAccessKeyName
        {
            get;
            private set;
        }

        public string SharedAccessKey
        {
            get;
            private set;
        }

        public string SharedAccessSignature
        {
            get;
            private set;
        }

        public string DeviceId
        {
            get;
            private set;
        }
        
        public string ModuleId
        {
            get;
            private set;
        }

        public string GatewayHostName
        {
            get;
            private set;
        }

        public string GetUser()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(this.SharedAccessKeyName);
            stringBuilder.Append(UserSeparator);
            stringBuilder.Append("sas.");
            stringBuilder.Append("root.");
            stringBuilder.Append(this.IotHubName);

            return stringBuilder.ToString();
        }

        public string GetPassword()
        {
            string password;
            if (string.IsNullOrWhiteSpace(this.SharedAccessSignature))
            {
                TimeSpan timeToLive;
                password = this.BuildToken(out timeToLive);
            }
            else
            { 
                password = this.SharedAccessSignature;
            }

            return password;
        }

        public string GetAuthorizationHeader()
        {
            return this.GetPassword();
        }

        Task<CbsToken> ICbsTokenProvider.GetTokenAsync(Uri namespaceAddress, string appliesTo, string[] requiredClaims)
        {
            string tokenValue;
            CbsToken token;
            if (string.IsNullOrWhiteSpace(this.SharedAccessSignature))
            {
                TimeSpan timeToLive;
                tokenValue = this.BuildToken(out timeToLive);
                token = new CbsToken(tokenValue, CbsConstants.IotHubSasTokenType, DateTime.UtcNow.Add(timeToLive));
            }
            else
            {
                tokenValue = this.SharedAccessSignature;
                token = new CbsToken(tokenValue, CbsConstants.IotHubSasTokenType, DateTime.MaxValue);
            }

            return Task.FromResult(token);
        }
        
        public Uri BuildLinkAddress(string path)
        {
            var builder = new UriBuilder(this.AmqpEndpoint)
            {
                Path = path,
            };

            return builder.Uri;
        }

        public static IotHubConnectionString Parse(string connectionString)
        {
            var builder = IotHubConnectionStringBuilder.Create(connectionString);
            return new IotHubConnectionString(builder);
        }

        string BuildToken(out TimeSpan ttl)
        {
            var builder = new SharedAccessSignatureBuilder()
            {
                KeyName = this.SharedAccessKeyName,
                Key = this.SharedAccessKey,
                TimeToLive = DefaultTokenTimeToLive,
                Target = this.Audience
            };

            if (this.DeviceId != null)
            {
#if NETMF
                if (this.ModuleId == null || this.ModuleId.Length == 0)
                {
                    builder.Target = this.Audience + "/devices/" + WebUtility.UrlEncode(this.DeviceId);
                }
                else 
                {
                    builder.Target = this.Audience + "/devices/" + WebUtility.UrlEncode(this.DeviceId) + "/modules/" + WebUtility.UrlEncode(this.ModuleId);
                }
#else
                if (string.IsNullOrEmpty(this.ModuleId))
                {
                    builder.Target = "{0}/devices/{1}".FormatInvariant(this.Audience, WebUtility.UrlEncode(this.DeviceId));
                }
                else
                {
                    builder.Target = "{0}/devices/{1}/modules/{2}".FormatInvariant(this.Audience, WebUtility.UrlEncode(this.DeviceId), WebUtility.UrlEncode(this.ModuleId));
                }
#endif
            }

            ttl = builder.TimeToLive;

            return builder.ToSignature();
        }
    }
}
