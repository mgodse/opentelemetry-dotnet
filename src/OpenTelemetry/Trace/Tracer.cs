﻿// <copyright file="Tracer.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace
{
    internal sealed class Tracer : ITracer
    {
        private readonly SpanProcessor spanProcessor;
        private readonly TracerConfiguration tracerConfiguration;
        private readonly Sampler sampler;

        static Tracer()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        /// <summary>
        /// Creates an instance of <see cref="Tracer"/>.
        /// </summary>
        /// <param name="spanProcessor">Span processor.</param>
        /// <param name="sampler">Sampler to use.</param>
        /// <param name="tracerConfiguration">Trace configuration.</param>
        /// <param name="binaryFormat">Binary format context propagator.</param>
        /// <param name="textFormat">Text format context propagator.</param>
        /// <param name="libraryResource">Resource describing the instrumentation library.</param>
        internal Tracer(SpanProcessor spanProcessor, Sampler sampler, TracerConfiguration tracerConfiguration, IBinaryFormat binaryFormat, ITextFormat textFormat, Resource libraryResource)
        {
            this.spanProcessor = spanProcessor ?? throw new ArgumentNullException(nameof(spanProcessor));
            this.tracerConfiguration = tracerConfiguration ?? throw new ArgumentNullException(nameof(tracerConfiguration));
            this.BinaryFormat = binaryFormat ?? throw new ArgumentNullException(nameof(binaryFormat));
            this.TextFormat = textFormat ?? throw new ArgumentNullException(nameof(textFormat));
            this.LibraryResource = libraryResource ?? throw new ArgumentNullException(nameof(libraryResource));
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        }

        public Resource LibraryResource { get; }

        /// <inheritdoc/>
        public ISpan CurrentSpan => (ISpan)Span.Current ?? BlankSpan.Instance;

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat { get; }

        /// <inheritdoc/>
        public ITextFormat TextFormat { get; }

        public IDisposable WithSpan(ISpan span, bool endSpanOnDispose)
        {
            if (span == null)
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("WithSpan", nameof(span), "is null");
            }

            if (span is Span spanImpl)
            {
                return spanImpl.BeginScope(endSpanOnDispose);
            }

            return NoopDisposable.Instance;
        }

        /// <inheritdoc/>
        public ISpan StartRootSpan(string operationName, SpanKind kind, SpanCreationOptions options)
        {
            return Span.CreateRoot(operationName, kind, options, this.sampler, this.tracerConfiguration, this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string operationName, ISpan parent, SpanKind kind, SpanCreationOptions options)
        {
            if (parent == null)
            {
                parent = this.CurrentSpan;
            }

            return Span.CreateFromParentSpan(operationName, parent, kind, options, this.sampler, this.tracerConfiguration,
                this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public ISpan StartSpan(string operationName, in SpanContext parent, SpanKind kind, SpanCreationOptions options)
        {
            if (parent != null)
            {
                return Span.CreateFromParentContext(operationName, parent, kind, options, this.sampler, this.tracerConfiguration,
                    this.spanProcessor, this.LibraryResource);
            }

            return Span.CreateRoot(operationName, kind, options, this.sampler, this.tracerConfiguration,
                this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public ISpan StartSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links)
        {
            bool isValidActivity = true;
            if (activity == null)
            {
                isValidActivity = false;
                OpenTelemetrySdkEventSource.Log.InvalidArgument("StartSpanFromActivity", nameof(activity), "is null");
            }
            else
            {
                if (activity.IdFormat != ActivityIdFormat.W3C)
                {
                    isValidActivity = false;
                    OpenTelemetrySdkEventSource.Log.InvalidArgument("StartSpanFromActivity", nameof(activity), "is not in W3C Trace-Context format");
                }

                if (activity.StartTimeUtc == default)
                {
                    isValidActivity = false;
                    OpenTelemetrySdkEventSource.Log.InvalidArgument("StartSpanFromActivity", nameof(activity), "is not started");
                }
            }

            if (!isValidActivity)
            {
                return this.StartSpan(operationName, kind, links != null ? new SpanCreationOptions { Links = links } : null);
            }

            return Span.CreateFromActivity(operationName, activity, kind, links, this.sampler, this.tracerConfiguration, this.spanProcessor, this.LibraryResource);
        }
    }
}
