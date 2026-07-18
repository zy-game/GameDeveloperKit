using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情运行时模块的 Program 接口。
    /// </summary>
    public sealed partial class StoryModule
    {
        private readonly Dictionary<string, Program> m_Programs =
            new Dictionary<string, Program>(StringComparer.Ordinal);

        /// <summary>
        /// 当前运行的剧情运行器。
        /// </summary>
        public Runner CurrentRunner { get; private set; }

        /// <summary>
        /// 当前运行的剧情程序。
        /// </summary>
        public Program CurrentProgram => CurrentRunner?.Program;

        /// <summary>
        /// 当前帧。
        /// </summary>
        public Frame CurrentFrame => CurrentRunner?.CurrentFrame;

        /// <summary>
        /// 当前变量存储。
        /// </summary>
        public IVariableStore VariableStore => CurrentRunner?.VariableStore;

        /// <summary>
        /// 当前外部函数解析器。
        /// </summary>
        public IFunctionResolver FunctionResolver { get; private set; }

        /// <summary>
        /// 设置外部函数解析器。
        /// </summary>
        /// <param name="resolver">外部函数解析器。</param>
        public void SetFunctionResolver(IFunctionResolver resolver)
        {
            FunctionResolver = resolver;
        }

        /// <summary>
        /// 注册一份剧情程序。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        public void Register(Program program)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            ValidateProgram(program);
            if (m_Programs.ContainsKey(program.StoryId))
            {
                throw new GameException($"Story program has already been registered. story:{program.StoryId}");
            }

            m_Programs.Add(program.StoryId, program);
        }

        /// <summary>
        /// 判断指定剧情程序是否已注册。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <returns>已注册时返回 true。</returns>
        public bool HasProgram(string storyId)
        {
            ValidateText(storyId, nameof(storyId), "Story id cannot be empty.");
            return m_Programs.ContainsKey(storyId);
        }

        /// <summary>
        /// 尝试获取已注册的剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="program">剧情程序。</param>
        /// <returns>获取成功时返回 true。</returns>
        public bool TryGetProgram(string storyId, out Program program)
        {
            ValidateText(storyId, nameof(storyId), "Story id cannot be empty.");
            return m_Programs.TryGetValue(storyId, out program);
        }

        /// <summary>
        /// 移除已注册的剧情程序。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <returns>成功移除时返回 true。</returns>
        public bool UnregisterProgram(string storyId)
        {
            ValidateText(storyId, nameof(storyId), "Story id cannot be empty.");
            return m_Programs.Remove(storyId);
        }

        /// <summary>
        /// 注册并启动指定剧情程序。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapterId">可选章节 ID。</param>
        /// <returns>启动后的运行器。</returns>
        public Runner Start(Program program, string chapterId = null)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            if (!m_Programs.ContainsKey(program.StoryId))
            {
                Register(program);
            }

            return StartProgram(program.StoryId, chapterId);
        }

        /// <summary>
        /// 从已注册的剧情程序启动新的运行器。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="chapterId">可选章节 ID。</param>
        /// <returns>启动后的运行器。</returns>
        public Runner StartProgram(string storyId, string chapterId = null)
        {
            ValidateText(storyId, nameof(storyId), "Story id cannot be empty.");
            if (!m_Programs.TryGetValue(storyId, out var program))
            {
                throw new GameException($"Story program is not registered. story:{storyId}");
            }

            var runner = new Runner(program, FunctionResolver);
            runner.Start(chapterId);
            ReplaceCurrentRunner(runner);
            return runner;
        }

        /// <summary>
        /// 继续当前剧情。
        /// </summary>
        /// <returns>当前帧。</returns>
        public Frame Continue()
        {
            EnsureRunner();
            return CurrentRunner.Continue();
        }

        /// <summary>
        /// 选择当前剧情的选项。
        /// </summary>
        /// <param name="choiceId">选项 ID。</param>
        /// <returns>选择后的帧。</returns>
        public Frame Select(string choiceId)
        {
            EnsureRunner();
            return CurrentRunner.Select(choiceId);
        }

        /// <summary>
        /// 完成当前剧情命令。
        /// </summary>
        /// <param name="commandId">命令 ID。</param>
        /// <param name="outcomeId">结果 ID。</param>
        /// <returns>完成后的帧。</returns>
        public Frame CompleteCommand(string commandId, string outcomeId)
        {
            EnsureRunner();
            return CurrentRunner.CompleteCommand(commandId, outcomeId);
        }

        /// <summary>
        /// 推进当前剧情等待时间。
        /// </summary>
        /// <param name="time">时间增量。</param>
        /// <returns>当前帧。</returns>
        public Frame Evaluate(double time)
        {
            EnsureRunner();
            return CurrentRunner.Evaluate(time);
        }

        /// <summary>
        /// 创建当前剧情快照。
        /// </summary>
        /// <returns>快照。</returns>
        public Snapshot CreateSnapshot()
        {
            EnsureRunner();
            return CurrentRunner.CreateSnapshot();
        }

        /// <summary>
        /// 从剧情快照恢复。
        /// </summary>
        /// <param name="snapshot">快照。</param>
        /// <returns>恢复后的运行器。</returns>
        public Runner Restore(Snapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (string.IsNullOrWhiteSpace(snapshot.StoryId))
            {
                throw new GameException("Story snapshot story id cannot be empty.");
            }

            if (!m_Programs.TryGetValue(snapshot.StoryId, out var program))
            {
                throw new GameException($"Story program is not registered. story:{snapshot.StoryId}");
            }

            var runner = new Runner(program, FunctionResolver);
            runner.Restore(snapshot);
            ReplaceCurrentRunner(runner);
            return runner;
        }

        private void ReplaceCurrentRunner(Runner runner)
        {
            if (CurrentRunner != null)
            {
                CurrentRunner = null;
            }

            CurrentRunner = runner;
        }

        private void EnsureRunner()
        {
            if (CurrentRunner == null)
            {
                throw new GameException("Story program has not started.");
            }
        }
    }
}
