﻿// <copyright file="TracerFactoryTest.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class TracerFactoryTest
    {
        [Fact]
        public void CreateFactory_NullBuilder()
        {
            Assert.Throws<ArgumentNullException>(() => TracerFactory.Create(null));
        }

        [Fact]
        public void CreateFactory_DefaultBuilder()
        {
            var tracerFactory = TracerFactory.Create(b => { });
            var tracer = tracerFactory.GetTracer("");
            Assert.NotNull(tracer);
            Assert.IsType<Tracer>(tracer);

            Assert.IsType<BinaryFormat>(tracer.BinaryFormat);
            Assert.IsType<TraceContextFormat>(tracer.TextFormat);

            var span = tracer.StartSpan("foo");
            Assert.NotNull(span);
            Assert.IsType<Span>(span);

            // default sampler is always sample
            Assert.True(span.IsRecording);
            Assert.Equal(Resource.Empty, ((Span)span).LibraryResource);
        }

        [Fact]
        public void CreateFactory_BuilderWithArgs()
        {
            var exporterCalledCount = 0;

            var testExporter = new TestExporter(spans =>
            {
                exporterCalledCount ++;
                Assert.Single(spans);
                Assert.IsType<Span>(spans.Single());
            });

            TestCollector collector1 = null;
            TestCollector collector2 = null;
            TestProcessor processor = null;
            var tracerFactory = TracerFactory.Create(b => b
                .AddProcessorPipeline(p => p
                    .SetExporter(testExporter)
                    .SetExportingProcessor(e =>
                    {
                        processor = new TestProcessor(e);
                        return processor;
                    }))
                .AddCollector(t =>
                {
                    collector1 = new TestCollector(t);
                    return collector1;
                })
                .AddCollector(t =>
                {
                    collector2 = new TestCollector(t);
                    return collector2;
                }));

            var tracer = tracerFactory.GetTracer("my-app");
            var span = tracer.StartSpan("foo");
            span.End();

            // default sampler is always sample
            Assert.True(span.IsRecording);
            Assert.Equal(1, exporterCalledCount);
            Assert.Single(((Span)span).LibraryResource.Attributes);
            Assert.Single(((Span)span).LibraryResource.Attributes.Where(kvp => kvp.Key == "name" && kvp.Value == "my-app"));

            Assert.NotNull(collector1);
            Assert.NotNull(collector2);
            Assert.NotNull(processor);

            var span1 = collector1.Collect();
            var span2 = collector1.Collect();

            Assert.Equal(3, exporterCalledCount);

            Assert.Equal(2, span1.LibraryResource.Attributes.Count());
            Assert.Equal(2, span2.LibraryResource.Attributes.Count());
            Assert.Single(span1.LibraryResource.Attributes.Where(kvp => kvp.Key == "name" && kvp.Value == "TestCollector"));
            Assert.Single(span2.LibraryResource.Attributes.Where(kvp => kvp.Key == "name" && kvp.Value == "TestCollector"));

            Assert.Single(span1.LibraryResource.Attributes.Where(kvp => kvp.Key == "version" && kvp.Value == "semver:1.0.0.0"));
            Assert.Single(span2.LibraryResource.Attributes.Where(kvp => kvp.Key == "version" && kvp.Value == "semver:1.0.0.0"));

            tracerFactory.Dispose();
            Assert.True(collector1.IsDisposed);
            Assert.True(collector2.IsDisposed);
            Assert.True(processor.IsDisposed);
        }


        [Fact]
        public void CreateFactory_BuilderWithMultiplePipelines()
        {
            var exporterCalledCount = 0;

            var testExporter = new TestExporter(spans =>
            {
                exporterCalledCount++;
                Assert.Single(spans);
                Assert.IsType<Span>(spans.Single());
            });

            var processCalledCount = 0;

            TestProcessor testProcessor1 = null;
            var testProcessor2 = new TestProcessor(_ => processCalledCount ++);
            
            var tracerFactory = TracerFactory.Create(b => b
                .AddProcessorPipeline(p => p
                    .SetExporter(testExporter)
                    .SetExportingProcessor(e =>
                    {
                        testProcessor1 = new TestProcessor(e);
                        return testProcessor1;
                    }))
                .AddProcessorPipeline(p => p
                    .AddProcessor(_ => testProcessor2)));

            var tracer = tracerFactory.GetTracer("my-app");
            var span = tracer.StartSpan("foo");
            span.End();

            Assert.Equal(1, exporterCalledCount);
            Assert.Equal(1, processCalledCount);

            tracerFactory.Dispose();
            Assert.True(testProcessor1.IsDisposed);
            Assert.True(testProcessor2.IsDisposed);
        }
        
        [Fact]
        public void GetTracer_NoName_NoVersion()
        {
            var tracerFactory = TracerFactory.Create(b => { });
            var tracer = (Tracer)tracerFactory.GetTracer("");
            Assert.DoesNotContain(tracer.LibraryResource.Attributes, kvp => kvp.Key == "name");
            Assert.DoesNotContain(tracer.LibraryResource.Attributes, kvp => kvp.Key == "version");
        }

        [Fact]
        public void GetTracer_NoName_Version()
        {
            var tracerFactory = TracerFactory.Create(b => { });
            var tracer = (Tracer)tracerFactory.GetTracer(null, "semver:1.0.0");
            Assert.DoesNotContain(tracer.LibraryResource.Attributes, kvp => kvp.Key == "name");
            Assert.DoesNotContain(tracer.LibraryResource.Attributes, kvp => kvp.Key == "version");
        }

        [Fact]
        public void GetTracer_Name_NoVersion()
        {
            var tracerFactory = TracerFactory.Create(b => { });
            var tracer = (Tracer)tracerFactory.GetTracer("foo");
            Assert.Equal("foo", tracer.LibraryResource.Attributes.Single(kvp => kvp.Key == "name").Value);
            Assert.DoesNotContain(tracer.LibraryResource.Attributes, kvp => kvp.Key == "version");
        }

        [Fact]
        public void GetTracer_Name_Version()
        {
            var tracerFactory = TracerFactory.Create(b => { });
            var tracer = (Tracer)tracerFactory.GetTracer("foo", "semver:1.2.3");
            Assert.Equal("foo", tracer.LibraryResource.Attributes.Single(kvp => kvp.Key == "name").Value);
            Assert.Equal("semver:1.2.3", tracer.LibraryResource.Attributes.Single(kvp => kvp.Key == "version").Value);
        }

        [Fact]
        public void GetTracerReturnsTracerWithResourceAfterSetResource()
        {
            var tracerFactory = TracerFactory.Create(b => { b.SetResource(new Resource(new Dictionary<string, string>() { { "a", "b" } })); });
            var tracer = (Tracer)tracerFactory.GetTracer("foo", "semver:1.2.3");
            Assert.Equal("b", tracer.LibraryResource.Attributes.Single(kvp => kvp.Key == "a").Value);
            Assert.Equal("foo", tracer.LibraryResource.Attributes.Single(kvp => kvp.Key == "name").Value);
            Assert.Equal("semver:1.2.3", tracer.LibraryResource.Attributes.Single(kvp => kvp.Key == "version").Value);
        }

        [Fact]
        public void GetTracerReturnsTracerWithResourceOverriddenBySetResource()
        {
            var tracerFactory = TracerFactory.Create(b => { 
                b.SetResource(new Resource(new Dictionary<string, string>() { { "a", "b" } }))
                .SetResource(new Resource(new Dictionary<string, string>() { { "a", "c" } })); });
            var tracer = (Tracer)tracerFactory.GetTracer("foo", "semver:1.2.3");
            Assert.Equal("c", tracer.LibraryResource.Attributes.Single(kvp => kvp.Key == "a").Value);
        }

        [Fact]
        public void FactoryReturnsSameTracerForGivenNameAndVersion()
        {
            var tracerFactory = TracerFactory.Create(b => { });
            var tracer1 = tracerFactory.GetTracer("foo", "semver:1.2.3");
            var tracer2 = tracerFactory.GetTracer("foo");
            var tracer3 = tracerFactory.GetTracer("foo", "semver:2.3.4");
            var tracer4 = tracerFactory.GetTracer("bar", "semver:1.2.3");
            var tracer5 = tracerFactory.GetTracer("foo", "semver:1.2.3");
            var tracer6 = tracerFactory.GetTracer("");
            var tracer7 = tracerFactory.GetTracer(null);
            var tracer8 = tracerFactory.GetTracer(null, "semver:1.2.3");

            Assert.NotEqual(tracer1, tracer2);
            Assert.NotEqual(tracer1, tracer3);
            Assert.NotEqual(tracer1, tracer4);
            Assert.Equal(tracer1, tracer5);
            Assert.NotEqual(tracer5, tracer6);
            Assert.Equal(tracer6, tracer7);
            Assert.Equal(tracer7, tracer8);
        }

        private class TestProcessor : SpanProcessor, IDisposable
        {
            private readonly SpanExporter exporter;
            private readonly Action<Span> onEnd;

            public bool IsDisposed { get; private set; }

            public TestProcessor(Action<Span> onEnd)
            {
                this.exporter = null;
                this.onEnd = onEnd;
            }

            public TestProcessor(SpanExporter exporter)
            {
                this.exporter = exporter;
                this.onEnd = null;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }

            public override void OnStart(Span span)
            {
            }

            public override void OnEnd(Span span)
            {
                this.onEnd?.Invoke(span);
                exporter?.ExportAsync(new[] {span}, default);
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private class TestCollector : IDisposable
        {
            private readonly ITracer tracer;
            public bool IsDisposed { get; private set; }

            public TestCollector(ITracer tracer)
            {
                this.tracer = tracer;
            }

            public Span Collect()
            {
                var span = this.tracer.StartSpan("foo");
                span.End();
                return (Span)span;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
