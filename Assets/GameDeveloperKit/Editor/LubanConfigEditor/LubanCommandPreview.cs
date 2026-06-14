using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Command Preview 类型。
    /// </summary>
    public sealed class LubanCommandPreview
    {
        private LubanCommandPreview()
        {
        }

        public string Command { get; private set; }

        public string Arguments { get; private set; }

        public string WorkingDirectory { get; private set; }

        public bool Generate { get; private set; }

        /// <summary>
        /// 创建 Check。
        /// </summary>
        /// <param name="releasePath">release Path 参数。</param>
        /// <param name="workspace">workspace 参数。</param>
        /// <param name="profile">profile 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanCommandPreview CreateCheck(string releasePath, LubanWorkspaceProfile workspace, LubanGenerationProfile profile)
        {
            return Create(releasePath, workspace, profile, false);
        }

        /// <summary>
        /// 创建 Generate。
        /// </summary>
        /// <param name="releasePath">release Path 参数。</param>
        /// <param name="workspace">workspace 参数。</param>
        /// <param name="profile">profile 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanCommandPreview CreateGenerate(string releasePath, LubanWorkspaceProfile workspace, LubanGenerationProfile profile)
        {
            return Create(releasePath, workspace, profile, true);
        }

        /// <summary>
        /// 创建。
        /// </summary>
        /// <param name="releasePath">release Path 参数。</param>
        /// <param name="workspace">workspace 参数。</param>
        /// <param name="profile">profile 参数。</param>
        /// <param name="generate">generate 参数。</param>
        /// <returns>执行结果。</returns>
        private static LubanCommandPreview Create(string releasePath, LubanWorkspaceProfile workspace, LubanGenerationProfile profile, bool generate)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var arguments = new List<string>
            {
                LubanCommandRunner.QuoteArgument(LubanCommandRunner.GetAbsoluteProjectPath(releasePath)),
                "--conf",
                LubanCommandRunner.QuoteArgument(workspace.ConfPath),
                "-t",
                LubanCommandRunner.QuoteArgument(GetTarget(workspace, profile))
            };

            AppendCommonArguments(arguments, profile);
            if (generate)
            {
                AppendGenerateArguments(arguments, profile);
            }
            else
            {
                arguments.Add("-f");
            }

            var dotnetArguments = string.Join(" ", arguments);
            return new LubanCommandPreview
            {
                Arguments = dotnetArguments,
                Command = $"dotnet {dotnetArguments}",
                WorkingDirectory = LubanCommandRunner.GetProjectRoot(),
                Generate = generate
            };
        }

        /// <summary>
        /// 获取 Target。
        /// </summary>
        /// <param name="workspace">workspace 参数。</param>
        /// <param name="profile">profile 参数。</param>
        /// <returns>执行结果。</returns>
        private static string GetTarget(LubanWorkspaceProfile workspace, LubanGenerationProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.Target) is false)
            {
                return profile.Target.Trim();
            }

            return string.IsNullOrWhiteSpace(workspace.DefaultTarget) ? "client" : workspace.DefaultTarget.Trim();
        }

        /// <summary>
        /// 追加 Common Arguments。
        /// </summary>
        /// <param name="arguments">arguments 参数。</param>
        /// <param name="profile">profile 参数。</param>
        private static void AppendCommonArguments(List<string> arguments, LubanGenerationProfile profile)
        {
            AppendOptional(arguments, "--pipeline", profile.Pipeline);
            AppendRepeated(arguments, "-i", profile.IncludeTag);
            AppendRepeated(arguments, "-e", profile.ExcludeTag);
            AppendOptional(arguments, "--variant", profile.Variant);
            if (profile.ValidationFailAsError)
            {
                arguments.Add("--validationFailAsError");
            }

            foreach (var xarg in SplitValues(profile.Xargs))
            {
                arguments.Add("-x");
                arguments.Add(LubanCommandRunner.QuoteArgument(xarg));
            }
        }

        /// <summary>
        /// 追加 Generate Arguments。
        /// </summary>
        /// <param name="arguments">arguments 参数。</param>
        /// <param name="profile">profile 参数。</param>
        private static void AppendGenerateArguments(List<string> arguments, LubanGenerationProfile profile)
        {
            AppendOptional(arguments, "-c", profile.CodeTarget);
            AppendOptional(arguments, "-d", profile.DataTarget);
            AppendCustomTemplateDir(arguments, profile);
            AppendXarg(arguments, "outputCodeDir", profile.OutputCodeDirectory);
            AppendXarg(arguments, "outputDataDir", profile.OutputDataDirectory);
            AppendOutputTables(arguments, profile);
        }

        /// <summary>
        /// 追加 Optional。
        /// </summary>
        /// <param name="arguments">arguments 参数。</param>
        /// <param name="name">name 参数。</param>
        /// <param name="value">value 参数。</param>
        private static void AppendOptional(List<string> arguments, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            arguments.Add(name);
            arguments.Add(LubanCommandRunner.QuoteArgument(value.Trim()));
        }

        /// <summary>
        /// 追加 Repeated。
        /// </summary>
        /// <param name="arguments">arguments 参数。</param>
        /// <param name="name">name 参数。</param>
        /// <param name="values">values 参数。</param>
        private static void AppendRepeated(List<string> arguments, string name, string values)
        {
            foreach (var value in SplitValues(values))
            {
                arguments.Add(name);
                arguments.Add(LubanCommandRunner.QuoteArgument(value));
            }
        }

        /// <summary>
        /// 追加 Xarg。
        /// </summary>
        /// <param name="arguments">arguments 参数。</param>
        /// <param name="key">key 参数。</param>
        /// <param name="value">value 参数。</param>
        private static void AppendXarg(List<string> arguments, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            arguments.Add("-x");
            arguments.Add(LubanCommandRunner.QuoteArgument($"{key}={value.Trim()}"));
        }

        /// <summary>
        /// 追加 Output Tables。
        /// </summary>
        /// <param name="arguments">arguments 参数。</param>
        /// <param name="profile">profile 参数。</param>
        private static void AppendOutputTables(List<string> arguments, LubanGenerationProfile profile)
        {
            if (profile.TableSelection.Scope != LubanTableScope.SelectedTables)
            {
                return;
            }

            foreach (var tableName in profile.TableSelection.SelectedTableNames)
            {
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    continue;
                }

                arguments.Add("-o");
                arguments.Add(LubanCommandRunner.QuoteArgument(tableName.Trim()));
            }
        }

        /// <summary>
        /// 追加 Custom Template Dir。
        /// </summary>
        /// <param name="arguments">arguments 参数。</param>
        /// <param name="profile">profile 参数。</param>
        private static void AppendCustomTemplateDir(List<string> arguments, LubanGenerationProfile profile)
        {
            if (profile.UseCustomTemplateDir is false || string.IsNullOrWhiteSpace(profile.CustomTemplateDirectory))
            {
                return;
            }

            arguments.Add("--customTemplateDir");
            arguments.Add(LubanCommandRunner.QuoteArgument(profile.CustomTemplateDirectory.Trim()));
        }

        /// <summary>
        /// 拆分 Values。
        /// </summary>
        /// <param name="values">values 参数。</param>
        /// <returns>执行结果。</returns>
        private static IEnumerable<string> SplitValues(string values)
        {
            if (string.IsNullOrWhiteSpace(values))
            {
                return Enumerable.Empty<string>();
            }

            return values
                .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Select(x => x.StartsWith("-x ", StringComparison.OrdinalIgnoreCase) ? x.Substring(3).Trim() : x)
                .Select(x => x.StartsWith("--xargs ", StringComparison.OrdinalIgnoreCase) ? x.Substring(8).Trim() : x)
                .Where(x => string.IsNullOrWhiteSpace(x) is false);
        }
    }
}
