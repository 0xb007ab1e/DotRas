﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

// ReSharper disable UnusedMethodReturnValue.Global
#pragma warning disable IDE0011
#pragma warning disable IDE0021
#pragma warning disable IDE0022

namespace DotRas.Internal.Infrastructure.IoC {
    /// <summary>
    /// Inversion of control container handles dependency injection for registered types
    /// </summary>
    internal class Container : Container.IScope {
        #region Public interfaces
        /// <summary>
        /// Represents a scope in which per-scope objects are instantiated a single time
        /// </summary>
        public interface IScope : IDisposable, IServiceProvider { }

        /// <summary>
        /// IRegisteredType is return by Container.Register and allows further configuration for the registration
        /// </summary>
        public interface IRegisteredType {
            /// <summary>
            /// Make registered type a singleton
            /// </summary>
            void AsSingleton();

            /// <summary>
            /// Make registered type a per-scope type (single instance within a Scope)
            /// </summary>
            void PerScope();
        }
        #endregion

        // Map of registered types
        private readonly Dictionary<Type, Func<ILifetime, object>> registeredTypes = new Dictionary<Type, Func<ILifetime, object>>();

        // Lifetime management
        private readonly ContainerLifetime lifetime;

        /// <summary>
        /// Creates a new instance of IoC Container
        /// </summary>
        public Container() => lifetime = new ContainerLifetime(t => registeredTypes[t]);

        /// <summary>
        /// Registers a factory function which will be called to resolve the specified interface
        /// </summary>
        /// <param name="interface">Interface to register</param>
        /// <param name="factory">Factory function</param>
        /// <returns></returns>
        public IRegisteredType Register(Type @interface, Func<object> factory) => RegisterType(@interface, _ => factory());

        /// <summary>
        /// Registers an implementation type for the specified interface
        /// </summary>
        /// <param name="interface">Interface to register</param>
        /// <param name="implementation">Implementing type</param>
        /// <returns></returns>
        public IRegisteredType Register(Type @interface, Type implementation) => RegisterType(@interface, FactoryFromType(implementation));

        private IRegisteredType RegisterType(Type itemType, Func<ILifetime, object> factory) => new RegisteredType(itemType, f => registeredTypes[itemType] = f, factory);

        /// <summary>
        /// Returns the object registered for the given type
        /// </summary>
        /// <param name="serviceType">Type as registered with the container</param>
        /// <returns>Instance of the registered type</returns>
        public object GetService(Type serviceType) => registeredTypes[serviceType](lifetime);

        /// <summary>
        /// Creates a new scope
        /// </summary>
        /// <returns>Scope object</returns>
        public IScope CreateScope() => new ScopeLifetime(lifetime);

        ~Container() {
            Dispose(false);
        }

        /// <summary>
        /// Disposes any <see cref="IDisposable"/> objects owned by this container.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                lifetime.Dispose();
            }
        }

        #region Lifetime management
        // ILifetime management adds resolution strategies to an IScope
        private interface ILifetime : IScope {
            object GetServiceAsSingleton(Type type, Func<ILifetime, object> factory);

            object GetServicePerScope(Type type, Func<ILifetime, object> factory);
        }

        // ObjectCache provides common caching logic for lifetimes
        private abstract class ObjectCache : IDisposable {
            // Instance cache
            private readonly ConcurrentDictionary<Type, object> instanceCache = new ConcurrentDictionary<Type, object>();

            // Get from cache or create and cache object
            protected object GetCached(Type type, Func<ILifetime, object> factory, ILifetime lifetime) => instanceCache.GetOrAdd(type, _ => factory(lifetime));

            ~ObjectCache() {
                Dispose(false);
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (disposing) {
                    foreach (var obj in instanceCache.Values)
                        (obj as IDisposable)?.Dispose();
                }
            }
        }

        // Container lifetime management
        private class ContainerLifetime : ObjectCache, ILifetime {
            // Retrieves the factory function from the given type, provided by owning container
            public Func<Type, Func<ILifetime, object>> GetFactory { get; }

            public ContainerLifetime(Func<Type, Func<ILifetime, object>> getFactory) => GetFactory = getFactory;

            public object GetService(Type serviceType) => GetFactory(serviceType)(this);

            // Singletons get cached per container
            public object GetServiceAsSingleton(Type type, Func<ILifetime, object> factory) => GetCached(type, factory, this);

            // At container level, per-scope items are equivalent to singletons
            public object GetServicePerScope(Type type, Func<ILifetime, object> factory) => GetServiceAsSingleton(type, factory);
        }

        // Per-scope lifetime management
        private class ScopeLifetime : ObjectCache, ILifetime {
            // Singletons come from parent container's lifetime
            private readonly ContainerLifetime parentLifetime;

            public ScopeLifetime(ContainerLifetime parentContainer) => parentLifetime = parentContainer;

            public object GetService(Type serviceType) => parentLifetime.GetFactory(serviceType)(this);

            // Singleton resolution is delegated to parent lifetime
            public object GetServiceAsSingleton(Type type, Func<ILifetime, object> factory) => parentLifetime.GetServiceAsSingleton(type, factory);

            // Per-scope objects get cached
            public object GetServicePerScope(Type type, Func<ILifetime, object> factory) => GetCached(type, factory, this);
        }
        #endregion

        #region Container items
        // Compiles a lambda that calls the given type's first constructor resolving arguments
        private static Func<ILifetime, object> FactoryFromType(Type itemType) {
            // Get first constructor for the type
            var constructors = itemType.GetConstructors();
            if (constructors.Length == 0) {
                // If no public constructor found, search for an internal constructor
                constructors = itemType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
            }
            var constructor = constructors.First();

            // Compile constructor call as a lambda expression
            var arg = Expression.Parameter(typeof(ILifetime));
            return (Func<ILifetime, object>)
                Expression
                    .Lambda(
                        Expression.New(
                            constructor,
                            constructor
                                .GetParameters()
                                .Select(param => {
                                    var resolve = new Func<ILifetime, object>(lifetime => lifetime.GetService(param.ParameterType));
                                    return Expression.Convert(Expression.Call(Expression.Constant(resolve.Target), resolve.Method, arg), param.ParameterType);
                                })
                        ),
                        arg
                    )
                    .Compile();
        }

        // RegisteredType is supposed to be a short lived object tying an item to its container
        // and allowing users to mark it as a singleton or per-scope item
        private class RegisteredType : IRegisteredType {
            private readonly Type itemType;
            private readonly Action<Func<ILifetime, object>> registerFactory;
            private readonly Func<ILifetime, object> factory;

            public RegisteredType(Type itemType, Action<Func<ILifetime, object>> registerFactory, Func<ILifetime, object> factory) {
                this.itemType = itemType;
                this.registerFactory = registerFactory;
                this.factory = factory;

                registerFactory(this.factory);
            }

            public void AsSingleton() => registerFactory(lifetime => lifetime.GetServiceAsSingleton(itemType, factory));

            public void PerScope() => registerFactory(lifetime => lifetime.GetServicePerScope(itemType, factory));
        }
        #endregion
    }

    /// <summary>
    /// Extension methods for Container
    /// </summary>
    internal static partial class ContainerExtensions {
        /// <summary>
        /// Registers an implementation type for the specified interface
        /// </summary>
        /// <typeparam name="T">Interface to register</typeparam>
        /// <param name="container">This container instance</param>
        /// <param name="type">Implementing type</param>
        /// <returns>IRegisteredType object</returns>
        public static Container.IRegisteredType Register<T>(this Container container, Type type) => container.Register(typeof(T), type);

        /// <summary>
        /// Registers an implementation type for the specified interface
        /// </summary>
        /// <typeparam name="TInterface">Interface to register</typeparam>
        /// <typeparam name="TImplementation">Implementing type</typeparam>
        /// <param name="container">This container instance</param>
        /// <returns>IRegisteredType object</returns>
        public static Container.IRegisteredType Register<TInterface, TImplementation>(this Container container) => container.Register(typeof(TInterface), typeof(TImplementation));

        /// <summary>
        /// Registers a factory function which will be called to resolve the specified interface
        /// </summary>
        /// <typeparam name="T">Interface to register</typeparam>
        /// <param name="container">This container instance</param>
        /// <param name="factory">Factory method</param>
        /// <returns>IRegisteredType object</returns>
        public static Container.IRegisteredType Register<T>(this Container container, Func<T> factory) => container.Register(typeof(T), () => factory());

        /// <summary>
        /// Registers a type
        /// </summary>
        /// <param name="container">This container instance</param>
        /// <typeparam name="T">Type to register</typeparam>
        /// <returns>IRegisteredType object</returns>
        public static Container.IRegisteredType Register<T>(this Container container) => container.Register(typeof(T), typeof(T));

        /// <summary>
        /// Returns an implementation of the specified interface
        /// </summary>
        /// <typeparam name="T">Interface type</typeparam>
        /// <param name="scope">This scope instance</param>
        /// <returns>Object implementing the interface</returns>
        public static T Resolve<T>(this Container.IScope scope) => (T)scope.GetService(typeof(T));
    }
}

#pragma warning restore IDE0022
#pragma warning restore IDE0021
#pragma warning restore IDE0011
