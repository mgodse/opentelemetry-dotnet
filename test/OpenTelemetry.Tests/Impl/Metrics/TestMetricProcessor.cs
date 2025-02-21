﻿// <copyright file="TestMetricProcessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics.Aggregators;

namespace OpenTelemetry.Metrics.Export
{
    internal class TestMetricProcessor : MetricProcessor
    {
        public List<Tuple<string, LabelSet, long>> counters = new List<Tuple<string, LabelSet, long>>();
        public List<Tuple<string, LabelSet, Tuple<long, DateTime>>> gauges = new List<Tuple<string, LabelSet, Tuple<long, DateTime>>>();
        public List<Tuple<string, LabelSet, List<long>>> measures = new List<Tuple<string, LabelSet, List<long>>>();

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<long> sumAggregator)
        {
            counters.Add(new Tuple<string, LabelSet, long>(metricName, labelSet, sumAggregator.ValueFromLastCheckpoint()));
        }

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<double> sumAggregator)
        {
        }

        public override void ProcessGauge(string meterName, string metricName, LabelSet labelSet, GaugeAggregator<long> gaugeAggregator)
        {
            gauges.Add(new Tuple<string, LabelSet, Tuple<long, DateTime>>(metricName, labelSet, gaugeAggregator.ValueFromLastCheckpoint()));
        }

        public override void ProcessGauge(string meterName, string metricName, LabelSet labelSet, GaugeAggregator<double> gaugeAggregator)
        {
        }

        public override void ProcessMeasure(string meterName, string metricName, LabelSet labelSet, MeasureExactAggregator<long> measureAggregator)
        {
            measures.Add(new Tuple<string, LabelSet, List<long>>(metricName, labelSet, measureAggregator.ValueFromLastCheckpoint()));
        }

        public override void ProcessMeasure(string meterName, string metricName, LabelSet labelSet, MeasureExactAggregator<double> measureAggregator)
        {
        }
    }
}
