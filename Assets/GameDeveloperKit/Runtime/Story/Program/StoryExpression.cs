using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情表达式类型。
    /// </summary>
    public enum StoryExpressionKind
    {
        /// <summary>
        /// 字面量。
        /// </summary>
        Literal = 0,

        /// <summary>
        /// 变量引用。
        /// </summary>
        Variable = 1,

        /// <summary>
        /// 外部函数。
        /// </summary>
        Function = 2,

        /// <summary>
        /// 非。
        /// </summary>
        Not = 3,

        /// <summary>
        /// 且。
        /// </summary>
        And = 4,

        /// <summary>
        /// 或。
        /// </summary>
        Or = 5,

        /// <summary>
        /// 等于。
        /// </summary>
        Equal = 6,

        /// <summary>
        /// 不等于。
        /// </summary>
        NotEqual = 7,

        /// <summary>
        /// 大于。
        /// </summary>
        Greater = 8,

        /// <summary>
        /// 大于等于。
        /// </summary>
        GreaterOrEqual = 9,

        /// <summary>
        /// 小于。
        /// </summary>
        Less = 10,

        /// <summary>
        /// 小于等于。
        /// </summary>
        LessOrEqual = 11
    }

    /// <summary>
    /// 剧情表达式。
    /// </summary>
    public sealed class StoryExpression
    {
        private readonly IReadOnlyList<StoryExpression> m_Inputs;

        private StoryExpression(
            StoryExpressionKind kind,
            StoryValue literal,
            string variableName,
            string functionName,
            IReadOnlyList<StoryExpression> inputs)
        {
            Kind = kind;
            Literal = literal;
            VariableName = variableName;
            FunctionName = functionName;
            m_Inputs = CopyList(inputs);
        }

        /// <summary>
        /// 表达式类型。
        /// </summary>
        public StoryExpressionKind Kind { get; }

        /// <summary>
        /// 字面量值。
        /// </summary>
        public StoryValue Literal { get; }

        /// <summary>
        /// 变量名。
        /// </summary>
        public string VariableName { get; }

        /// <summary>
        /// 函数名。
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// 输入表达式。
        /// </summary>
        public IReadOnlyList<StoryExpression> Inputs => m_Inputs;

        /// <summary>
        /// 创建字面量表达式。
        /// </summary>
        /// <param name="value">字面量。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression FromLiteral(StoryValue value)
        {
            return new StoryExpression(StoryExpressionKind.Literal, value, null, null, null);
        }

        /// <summary>
        /// 创建变量引用表达式。
        /// </summary>
        /// <param name="variableName">变量名。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression FromVariable(string variableName)
        {
            ValidateText(variableName, nameof(variableName));
            return new StoryExpression(StoryExpressionKind.Variable, default(StoryValue), variableName, null, null);
        }

        /// <summary>
        /// 创建外部函数表达式。
        /// </summary>
        /// <param name="functionName">函数名。</param>
        /// <param name="arguments">函数参数。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression FromFunction(string functionName, params StoryExpression[] arguments)
        {
            ValidateText(functionName, nameof(functionName));
            return new StoryExpression(StoryExpressionKind.Function, default(StoryValue), null, functionName, arguments);
        }

        /// <summary>
        /// 创建非表达式。
        /// </summary>
        /// <param name="input">输入表达式。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateNot(StoryExpression input)
        {
            ValidateSingleInput(input, nameof(input));
            return new StoryExpression(StoryExpressionKind.Not, default(StoryValue), null, null, new[] { input });
        }

        /// <summary>
        /// 创建且表达式。
        /// </summary>
        /// <param name="inputs">输入表达式。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateAnd(params StoryExpression[] inputs)
        {
            ValidateBinaryInputs(inputs, nameof(inputs), StoryExpressionKind.And);
            return new StoryExpression(StoryExpressionKind.And, default(StoryValue), null, null, inputs);
        }

        /// <summary>
        /// 创建或表达式。
        /// </summary>
        /// <param name="inputs">输入表达式。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateOr(params StoryExpression[] inputs)
        {
            ValidateBinaryInputs(inputs, nameof(inputs), StoryExpressionKind.Or);
            return new StoryExpression(StoryExpressionKind.Or, default(StoryValue), null, null, inputs);
        }

        /// <summary>
        /// 创建等于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateEqual(StoryExpression left, StoryExpression right)
        {
            return CreateBinary(StoryExpressionKind.Equal, left, right);
        }

        /// <summary>
        /// 创建不等于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateNotEqual(StoryExpression left, StoryExpression right)
        {
            return CreateBinary(StoryExpressionKind.NotEqual, left, right);
        }

        /// <summary>
        /// 创建大于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateGreater(StoryExpression left, StoryExpression right)
        {
            return CreateBinary(StoryExpressionKind.Greater, left, right);
        }

        /// <summary>
        /// 创建大于等于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateGreaterOrEqual(StoryExpression left, StoryExpression right)
        {
            return CreateBinary(StoryExpressionKind.GreaterOrEqual, left, right);
        }

        /// <summary>
        /// 创建小于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateLess(StoryExpression left, StoryExpression right)
        {
            return CreateBinary(StoryExpressionKind.Less, left, right);
        }

        /// <summary>
        /// 创建小于等于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static StoryExpression CreateLessOrEqual(StoryExpression left, StoryExpression right)
        {
            return CreateBinary(StoryExpressionKind.LessOrEqual, left, right);
        }

        private static StoryExpression CreateBinary(StoryExpressionKind kind, StoryExpression left, StoryExpression right)
        {
            ValidateSingleInput(left, nameof(left));
            ValidateSingleInput(right, nameof(right));
            return new StoryExpression(kind, default(StoryValue), null, null, new[] { left, right });
        }

        private static void ValidateSingleInput(StoryExpression input, string parameterName)
        {
            if (input == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        private static void ValidateBinaryInputs(IReadOnlyList<StoryExpression> inputs, string parameterName, StoryExpressionKind kind)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (inputs.Count < 2)
            {
                throw new ArgumentException($"Story expression '{kind}' requires at least two inputs.", parameterName);
            }

            for (var i = 0; i < inputs.Count; i++)
            {
                if (inputs[i] == null)
                {
                    throw new ArgumentNullException(parameterName);
                }
            }
        }

        private static IReadOnlyList<StoryExpression> CopyList(IReadOnlyList<StoryExpression> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<StoryExpression>();
            }

            return new List<StoryExpression>(items);
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }
}
