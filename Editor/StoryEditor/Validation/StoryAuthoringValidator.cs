using System;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// Story authoring asset 校验器。
    /// </summary>
    public static class StoryAuthoringValidator
    {
        public static StoryValidationReport Validate(StoryAuthoringAsset asset)
        {
            return StoryProgramCompiler.Validate(asset);
        }
    }
}
