using System;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Compiler;

namespace GameDeveloperKit.StoryEditor.Validation
{
    /// <summary>
    /// Story authoring asset 校验器。
    /// </summary>
    public static class AuthoringValidator
    {
        public static ValidationReport Validate(AuthoringAsset asset)
        {
            return ProgramCompiler.Validate(asset);
        }
    }
}
