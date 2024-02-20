﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental;

public class BuildAnalyzerConfiguration
{
    public static BuildAnalyzerConfiguration Default { get; } = new()
    {
        LifeTimeScope = Experimental.LifeTimeScope.PerProject,
        EvaluationAnalysisScope = Experimental.EvaluationAnalysisScope.AnalyzedProjectOnly,
        Severity = BuildAnalyzerResultSeverity.Info,
        IsEnabled = false,
    };

    public static BuildAnalyzerConfiguration Null { get; } = new();

    public LifeTimeScope? LifeTimeScope { get; internal init; }
    public EvaluationAnalysisScope? EvaluationAnalysisScope { get; internal init; }
    public BuildAnalyzerResultSeverity? Severity { get; internal init; }
    public bool? IsEnabled { get; internal init; }
}