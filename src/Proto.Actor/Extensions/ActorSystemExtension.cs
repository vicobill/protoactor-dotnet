// -----------------------------------------------------------------------
// <copyright file="IActorSystemExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;

namespace Proto.Extensions
{
    public interface IActorSystemExtension
    {
        private static int nextId;

        IReadOnlyCollection<Type> GetDependencies();

        internal static int GetNextId() => Interlocked.Increment(ref nextId);
    }

    // ReSharper disable once UnusedTypeParameter
    public abstract class ActorSystemExtension<T> : IActorSystemExtension where T : IActorSystemExtension
    {
        public static int Id = IActorSystemExtension.GetNextId();

        private readonly List<Type> _dependencies = new();

        public IReadOnlyCollection<Type> GetDependencies() => _dependencies;

        protected void AddDependency<TDep>() => _dependencies.Add(typeof(TDep));
    }
}