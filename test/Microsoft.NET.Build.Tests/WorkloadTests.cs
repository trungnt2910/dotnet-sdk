﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Versioning;

namespace Microsoft.NET.Build.Tests
{
    public class WorkloadTests : SdkTest
    {
        public WorkloadTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_should_build_with_workload()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-workloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_should_fail_without_workload()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-missingworkloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1147");
        }

        [Fact]
        public void It_should_create_suggested_workload_items()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-missingworkloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            var getValuesCommand = new GetValuesCommand(testAsset, "SuggestedWorkload", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "GetSuggestedWorkloads"
            };
            getValuesCommand.MetadataNames.Add("VisualStudioComponentId");
            getValuesCommand.MetadataNames.Add("VisualStudioComponentIds");
            getValuesCommand.ShouldRestore = false;

            getValuesCommand.Execute()
                .Should()
                .Pass();

            getValuesCommand.GetValuesWithMetadata().Select(valueAndMetadata => (valueAndMetadata.value, valueAndMetadata.metadata["VisualStudioComponentId"]))
                .Should()
                .BeEquivalentTo(new[] { ("microsoft-net-sdk-missingtestworkload", "microsoft.net.sdk.missingtestworkload") });

            getValuesCommand.GetValuesWithMetadata().Select(valueAndMetadata => (valueAndMetadata.value, valueAndMetadata.metadata["VisualStudioComponentIds"]))
                .Should()
                .BeEquivalentTo(new[] { ("microsoft-net-sdk-missingtestworkload", "microsoft.net.sdk.missingtestworkload") });
        }

        [Fact]
        public void It_should_fail_to_restore_without_workload_when_multitargeted()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-android;{ToolsetInfo.CurrentTargetFramework}-ios"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1147");

            //  Until https://github.com/NuGet/Home/issues/10872 is fixed, only one of the errors will be reported when restoring
            //  Once that is fixed we should add the following checks:
            //  .And
            //  .HaveStdOutContaining("ios")
            //  .And
            //  .HaveStdOutContaining("android");
        }

        [Fact]
        public void It_should_fail_to_build_without_workload_when_multitargeted()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-android;{ToolsetInfo.CurrentTargetFramework}-ios"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .ExecuteWithoutRestore()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1147")
                .And
                .HaveStdOutContaining("android");
        }

        [Fact]
        public void It_should_fail_to_build_when_multitargeted_to_unknown_platforms()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-foo;{ToolsetInfo.CurrentTargetFramework}-bar"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1139");
        }


        [Fact]
        public void It_should_fail_with_resolver_disabled()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-android"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            //  NETSDK1208: The target platform identifier android was not recognized.
            new BuildCommand(testAsset)
                .WithEnvironmentVariable("MSBuildEnableWorkloadResolver", "false")
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1208");
        }

        [Fact]
        public void It_should_import_AutoImports_for_installed_workloads()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var expectedProperty = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "WinTestWorkloadAutoImportPropsImported" : "UnixTestWorkloadAutoImportPropsImported";

            var getValuesCommand = new GetValuesCommand(testAsset, expectedProperty);

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo("true");
        }

        [Fact]
        public void It_should_import_aliased_pack()
        {
            var testProject = new TestProject()
            {
                Name = "WorkloadTest",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-workloadtestplatform"
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject);

            var expectedProperty = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                "UsingWinTestWorkloadPack" :
                "UsingUnixTestWorkloadPack";

            var getValuesCommand = new GetValuesCommand(testAsset, expectedProperty);

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo("true");
        }

        [Fact]
        public void It_should_get_suggested_workload_by_GetRequiredWorkloads_target()
        {
            var mainProject = new TestProject()
            {
                Name = "MainProject",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-android",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(mainProject);

            var getValuesCommand =
                new GetValuesCommand(testAsset, "_ResolvedSuggestedWorkload", GetValuesCommand.ValueType.Item)
                {
                    DependsOnTargets = "_GetRequiredWorkloads",
                    ShouldRestore = false
                };

            getValuesCommand.Execute("/p:SkipResolvePackageAssets=true")
                .Should()
                .Pass();

            getValuesCommand.GetValues()
                .Should()
                .BeEquivalentTo("android");
        }

        [Theory]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-android;{ToolsetInfo.CurrentTargetFramework}-ios", $"{ToolsetInfo.CurrentTargetFramework}-android;{ToolsetInfo.CurrentTargetFramework}-ios", "android;ios")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, $"{ToolsetInfo.CurrentTargetFramework};{ToolsetInfo.CurrentTargetFramework}-android;{ToolsetInfo.CurrentTargetFramework}-ios", "android;ios")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework};{ToolsetInfo.CurrentTargetFramework}-ios", $"{ToolsetInfo.CurrentTargetFramework};{ToolsetInfo.CurrentTargetFramework}-android", "android;ios")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFramework, null)]
        public void Given_multi_target_It_should_get_suggested_workload_by_GetRequiredWorkloads_target(string mainTfm, string referencingTfm, string expected)
        {
            // Skip Test if SDK is < 6.0.400
            var sdkVersion = SemanticVersion.Parse(TestContext.Current.ToolsetUnderTest.SdkVersion);
            if (new SemanticVersion(sdkVersion.Major, sdkVersion.Minor, sdkVersion.Patch) < new SemanticVersion(6, 0, 400))
                return; // MAUI was removed from earlier versions of the SDK

            var mainProject = new TestProject()
            {
                Name = "MainProject",
                TargetFrameworks = mainTfm,
                IsSdkProject = true,
                IsExe = true
            };

            var referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = referencingTfm,
                IsSdkProject = true,
            };
            referencedProject.AdditionalProperties["RunAOTCompilation"] = "true";

            mainProject.ReferencedProjects.Add(referencedProject);


            var testAsset = _testAssetsManager
                .CreateTestProject(mainProject, identifier: mainTfm + "_" + referencingTfm);

            var getValuesCommand =
                new GetValuesCommand(testAsset, "_ResolvedSuggestedWorkload", GetValuesCommand.ValueType.Item)
                {
                    DependsOnTargets = "_GetRequiredWorkloads",
                    ShouldRestore = false
                };

            getValuesCommand.Execute("/p:SkipResolvePackageAssets=true")
                .Should()
                .Pass();

            if (expected == null)
            {
                getValuesCommand.GetValues()
                    .Should()
                    .BeEmpty();
            }
            else
            {
                // Conditionally check the OS and modify the expected workloads on Linux
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    expected = "android;wasm-tools";
                }
                
                getValuesCommand.GetValues()
                    .Should()
                    .Contain(expected.Split(";")); // there are extra workloads in certain platform, only assert contains
            }
        }
    }
}
