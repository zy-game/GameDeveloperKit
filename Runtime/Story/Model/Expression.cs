using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情表达式类型。
    /// </summary>
    public enum ExpressionKind
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
    public sealed class Expression
    {
        private readonly IReadOnlyList<Expression> m_Inputs;

        private Expression(
            ExpressionKind kind,
            Value literal,
            string variableName,
            string functionName,
            IReadOnlyList<Expression> inputs)
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
        public ExpressionKind Kind { get; }

        /// <summary>
        /// 字面量值。
        /// </summary>
        public Value Literal { get; }

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
        public IReadOnlyList<Expression> Inputs => m_Inputs;

        /// <summary>
        /// 创建字面量表达式。
        /// </summary>
        /// <param name="value">字面量。</param>
        /// <returns>表达式。</returns>
        public static Expression FromLiteral(Value value)
        {
            return new Expression(ExpressionKind.Literal, value, null, null, null);
        }

        /// <summary>
        /// 创建变量引用表达式。
        /// </summary>
        /// <param name="variableName">变量名。</param>
        /// <returns>表达式。</returns>
        public static Expression FromVariable(string variableName)
        {
            ValidateText(variableName, nameof(variableName));
            return new Expression(ExpressionKind.Variable, default(Value), variableName, null, null);
        }

        /// <summary>
        /// 创建外部函数表达式。
        /// </summary>
        /// <param name="functionName">函数名。</param>
        /// <param name="arguments">函数参数。</param>
        /// <returns>表达式。</returns>
        public static Expression FromFunction(string functionName, params Expression[] arguments)
        {
            ValidateText(functionName, nameof(functionName));
            return new Expression(ExpressionKind.Function, default(Value), null, functionName, arguments);
        }

        /// <summary>
        /// 创建非表达式。
        /// </summary>
        /// <param name="input">输入表达式。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateNot(Expression input)
        {
            ValidateSingleInput(input, nameof(input));
            return new Expression(ExpressionKind.Not, default(Value), null, null, new[] { input });
        }

        /// <summary>
        /// 创建且表达式。
        /// </summary>
        /// <param name="inputs">输入表达式。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateAnd(params Expression[] inputs)
        {
            ValidateBinaryInputs(inputs, nameof(inputs), ExpressionKind.And);
            return new Expression(ExpressionKind.And, default(Value), null, null, inputs);
        }

        /// <summary>
        /// 创建或表达式。
        /// </summary>
        /// <param name="inputs">输入表达式。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateOr(params Expression[] inputs)
        {
            ValidateBinaryInputs(inputs, nameof(inputs), ExpressionKind.Or);
            return new Expression(ExpressionKind.Or, default(Value), null, null, inputs);
        }

        /// <summary>
        /// 创建等于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateEqual(Expression left, Expression right)
        {
            return CreateBinary(ExpressionKind.Equal, left, right);
        }

        /// <summary>
        /// 创建不等于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateNotEqual(Expression left, Expression right)
        {
            return CreateBinary(ExpressionKind.NotEqual, left, right);
        }

        /// <summary>
        /// 创建大于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateGreater(Expression left, Expression right)
        {
            return CreateBinary(ExpressionKind.Greater, left, right);
        }

        /// <summary>
        /// 创建大于等于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateGreaterOrEqual(Expression left, Expression right)
        {
            return CreateBinary(ExpressionKind.GreaterOrEqual, left, right);
        }

        /// <summary>
        /// 创建小于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateLess(Expression left, Expression right)
        {
            return CreateBinary(ExpressionKind.Less, left, right);
        }

        /// <summary>
        /// 创建小于等于表达式。
        /// </summary>
        /// <param name="left">左值。</param>
        /// <param name="right">右值。</param>
        /// <returns>表达式。</returns>
        public static Expression CreateLessOrEqual(Expression left, Expression right)
        {
            return CreateBinary(ExpressionKind.LessOrEqual, left, right);
        }

        private static Expression CreateBinary(ExpressionKind kind, Expression left, Expression right)
        {
            ValidateSingleInput(left, nameof(left));
            ValidateSingleInput(right, nameof(right));
            return new Expression(kind, default(Value), null, null, new[] { left, right });
        }

        private static void ValidateSingleInput(Expression input, string parameterName)
        {
            if (input == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        private static void ValidateBinaryInputs(IReadOnlyList<Expression> inputs, string parameterName, ExpressionKind kind)
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

        private static IReadOnlyList<Expression> CopyList(IReadOnlyList<Expression> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<Expression>();
            }

            return new List<Expression>(items);
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
