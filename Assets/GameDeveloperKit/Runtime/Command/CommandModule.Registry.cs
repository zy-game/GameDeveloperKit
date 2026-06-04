using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Command
{
    public sealed partial class CommandModule
    {
        private readonly Dictionary<string, Func<IReadOnlyList<object>, ICommand>> m_CommandFactories =
            new Dictionary<string, Func<IReadOnlyList<object>, ICommand>>(StringComparer.Ordinal);

        /// <summary>
        /// 注册命令工厂。
        /// </summary>
        /// <param name="commandName">命令名。</param>
        /// <param name="factory">命令工厂。</param>
        /// <returns>注册成功返回 true；同名命令已存在时返回 false。</returns>
        public bool Register(string commandName, Func<IReadOnlyList<object>, ICommand> factory)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException("Command name cannot be empty.", nameof(commandName));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (m_CommandFactories.ContainsKey(commandName))
            {
                return false;
            }

            m_CommandFactories.Add(commandName, factory);
            return true;
        }

        /// <summary>
        /// 注册带 <see cref="CommandAttribute"/> 的命令类型。
        /// </summary>
        /// <typeparam name="TCommand">命令类型。</typeparam>
        /// <returns>注册成功返回 true；同名命令已存在时返回 false。</returns>
        public bool Register<TCommand>() where TCommand : ICommand
        {
            return Register(typeof(TCommand));
        }

        /// <summary>
        /// 注册带 <see cref="CommandAttribute"/> 的命令类型。
        /// </summary>
        /// <param name="commandType">命令类型。</param>
        /// <returns>注册成功返回 true；同名命令已存在时返回 false。</returns>
        public bool Register(Type commandType)
        {
            if (commandType == null)
            {
                throw new ArgumentNullException(nameof(commandType));
            }

            if (!typeof(ICommand).IsAssignableFrom(commandType))
            {
                throw new ArgumentException("Command type must implement ICommand.", nameof(commandType));
            }

            var attribute = commandType.GetCustomAttribute<CommandAttribute>(false);
            if (attribute == null)
            {
                throw new ArgumentException("Command type must declare CommandAttribute.", nameof(commandType));
            }

            return Register(attribute.Name, args => CreateCommand(commandType, args));
        }

        /// <summary>
        /// 注销命令工厂。
        /// </summary>
        /// <param name="commandName">命令名。</param>
        /// <returns>命令存在并被移除时返回 true。</returns>
        public bool Unregister(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                return false;
            }

            return m_CommandFactories.Remove(commandName);
        }

        /// <summary>
        /// 通过命令名创建并执行命令。
        /// </summary>
        /// <param name="commandName">命令名。</param>
        /// <param name="args">创建命令所需参数。</param>
        /// <returns>命令执行结果。</returns>
        public async UniTask<CommandInvokeResult> ExecuteAsync(string commandName, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                return CommandInvokeResult.Failed(commandName, "Command name cannot be empty.");
            }

            if (!m_CommandFactories.TryGetValue(commandName, out var factory))
            {
                return CommandInvokeResult.Failed(commandName, $"Command '{commandName}' is not registered.");
            }

            try
            {
                var command = factory(args ?? Array.Empty<object>());
                if (command == null)
                {
                    return CommandInvokeResult.Failed(commandName, $"Command '{commandName}' factory returned null.");
                }

                await ExecuteAsync(command);
                return CommandInvokeResult.Success(commandName, command);
            }
            catch (Exception exception)
            {
                return CommandInvokeResult.Failed(commandName, exception.Message, exception);
            }
        }

        private static ICommand CreateCommand(Type commandType, IReadOnlyList<object> args)
        {
            var constructors = commandType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length != args.Count)
                {
                    continue;
                }

                if (TryBindParameters(parameters, args, out var boundArgs))
                {
                    return (ICommand)constructor.Invoke(boundArgs);
                }
            }

            throw new GameException($"Command '{commandType.Name}' has no constructor matching {args.Count} argument(s).");
        }

        private static bool TryBindParameters(ParameterInfo[] parameters, IReadOnlyList<object> args, out object[] boundArgs)
        {
            boundArgs = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!TryConvertArgument(args[i], parameters[i].ParameterType, out boundArgs[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryConvertArgument(object value, Type targetType, out object converted)
        {
            converted = null;
            if (value == null)
            {
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
            }

            var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (effectiveType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            try
            {
                if (effectiveType.IsEnum)
                {
                    converted = value is string text
                        ? Enum.Parse(effectiveType, text, true)
                        : Enum.ToObject(effectiveType, value);
                    return true;
                }

                converted = Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }
        }

        private void ClearCommandRegistry()
        {
            m_CommandFactories.Clear();
        }
    }
}
