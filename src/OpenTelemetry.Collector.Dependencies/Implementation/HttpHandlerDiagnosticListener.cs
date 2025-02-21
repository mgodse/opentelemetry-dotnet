﻿// <copyright file="HttpHandlerDiagnosticListener.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Collector.Dependencies.Implementation
{
    internal class HttpHandlerDiagnosticListener : ListenerHandler
    {
        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");
        private readonly PropertyFetcher stopResponseFetcher = new PropertyFetcher("Response");
        private readonly PropertyFetcher stopExceptionFetcher = new PropertyFetcher("Exception");
        private readonly PropertyFetcher stopRequestStatusFetcher = new PropertyFetcher("RequestTaskStatus");
        private readonly bool httpClientSupportsW3C = false;
        private readonly HttpClientCollectorOptions options;

        public HttpHandlerDiagnosticListener(ITracer tracer, HttpClientCollectorOptions options)
            : base("HttpHandlerDiagnosticListener", tracer)
        {
            var framework = Assembly
                .GetEntryAssembly()?
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;

            // Depending on the .NET version/flavor this will look like
            // '.NETCoreApp,Version=v3.0', '.NETCoreApp,Version = v2.2' or '.NETFramework,Version = v4.7.1'

            if (framework != null && framework.Contains("Version=v3"))
            {
                this.httpClientSupportsW3C = true;
            }

            this.options = options;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStartActivity";
            if (!(this.startRequestFetcher.Fetch(payload) is HttpRequestMessage request))
            {
                CollectorEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener) + EventNameSuffix);
                return;
            }

            if (request.Headers.Contains("traceparent"))
            {
                // this request is already instrumented, we should back off
                return;
            }

            this.Tracer.StartActiveSpanFromActivity(request.RequestUri.AbsolutePath, activity, SpanKind.Client, out var span);

            if (span.IsRecording)
            {
                span.PutComponentAttribute("http");
                span.PutHttpMethodAttribute(request.Method.ToString());
                span.PutHttpHostAttribute(request.RequestUri.Host, request.RequestUri.Port);
                span.PutHttpRawUrlAttribute(request.RequestUri.OriginalString);

                if (this.options.SetHttpFlavor)
                {
                    span.PutHttpFlavorAttribute(request.Version.ToString());
                }
            }

            if (!this.httpClientSupportsW3C)
            {
                this.Tracer.TextFormat.Inject<HttpRequestMessage>(span.Context, request, (r, k, v) => r.Headers.Add(k, v));
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStopActivity";
            var span = this.Tracer.CurrentSpan;

            if (span == null || span == BlankSpan.Instance)
            {
                CollectorEventSource.Log.NullOrBlankSpan(nameof(HttpHandlerDiagnosticListener) + EventNameSuffix);
                return;
            }

            if (span.IsRecording)
            {
                var requestTaskStatus = this.stopRequestStatusFetcher.Fetch(payload) as TaskStatus?;

                if (requestTaskStatus.HasValue)
                {
                    if (requestTaskStatus != TaskStatus.RanToCompletion)
                    {
                        span.Status = Status.Unknown;

                        if (requestTaskStatus == TaskStatus.Canceled)
                        {
                            span.Status = Status.Cancelled;
                        }
                    }
                }

                if (this.stopResponseFetcher.Fetch(payload) is HttpResponseMessage response)
                {
                    // response could be null for DNS issues, timeouts, etc...
                    span.PutHttpStatusCode((int)response.StatusCode, response.ReasonPhrase);
                }
            }

            span.End();
        }

        public override void OnException(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnException";
            var span = this.Tracer.CurrentSpan;

            if (span == null || span == BlankSpan.Instance)
            {
                CollectorEventSource.Log.NullOrBlankSpan(nameof(HttpHandlerDiagnosticListener) + EventNameSuffix);
                return;
            }

            if (span.IsRecording)
            {
                if (!(this.stopExceptionFetcher.Fetch(payload) is Exception exc))
                {
                    CollectorEventSource.Log.NullPayload(nameof(HttpHandlerDiagnosticListener) + EventNameSuffix);
                    return;
                }

                if (exc is HttpRequestException)
                {
                    // TODO: on netstandard this will be System.Net.Http.WinHttpException: The server name or address could not be resolved
                    if (exc.InnerException is WebException exception &&
                        exception.Status == WebExceptionStatus.NameResolutionFailure)
                    {
                        span.Status = Status.InvalidArgument;
                    }
                    else if (exc.InnerException != null)
                    {
                        span.Status = Status.Unknown.WithDescription(exc.Message);
                    }
                }
            }
        }
    }
}
