﻿// <copyright file="BasicMonsterIntelligence.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.NPC
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// A basic monster AI which is pretty basic.
    /// </summary>
    public sealed class BasicMonsterIntelligence : INpcIntelligence, IDisposable
    {
        private readonly GameMap map;

        private IAttackable currentTarget;

        private Timer aiTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicMonsterIntelligence"/> class.
        /// </summary>
        /// <param name="map">The map.</param>
        public BasicMonsterIntelligence(GameMap map)
        {
            this.map = map;
        }

        /// <inheritdoc/>
        public NonPlayerCharacter Npc
        {
            get => this.Monster;
            set => this.Monster = (Monster)value;
        }

        /// <summary>
        /// Gets or sets the monster.
        /// </summary>
        public Monster Monster { get; set; }

        /// <inheritdoc/>
        public void Start()
        {
            this.aiTimer = new Timer(state => this.Tick(), null, this.Npc.Definition.AttackDelay, this.Npc.Definition.AttackDelay);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.aiTimer != null)
            {
                this.aiTimer.Dispose();
                this.aiTimer = null;
            }
        }

        /// <inheritdoc/>
        public void RegisterHit(IAttacker attacker)
        {
            if (this.currentTarget == null && attacker is IAttackable attackable)
            {
                this.currentTarget = attackable;
            }
        }

        private IAttackable SearchNextTarget()
        {
            List<IWorldObserver> tempObservers;
            this.Npc.ObserverLock.EnterReadLock();
            try
            {
                tempObservers = new List<IWorldObserver>(this.Npc.Observers);
            }
            finally
            {
                this.Npc.ObserverLock.ExitReadLock();
            }

            double closestdistance = 100;
            IAttackable closest = null;

            foreach (var attackable in tempObservers.OfType<IAttackable>())
            {
                if (this.map.Terrain.SafezoneMap[attackable.Position.X, attackable.Position.Y])
                {
                    continue;
                }

                double d = attackable.GetDistanceTo(this.Npc);
                if (closestdistance > d)
                {
                    closest = attackable;
                    closestdistance = d;
                }
            }

            return closest;

            // todo: check the walk distance
        }

        private bool IsTargetInObservers()
        {
            this.Npc.ObserverLock.EnterReadLock();
            try
            {
                return this.Npc.Observers.Contains(this.currentTarget as IWorldObserver);
            }
            finally
            {
                this.Npc.ObserverLock.ExitReadLock();
            }
        }

        private void Tick()
        {
            if (!this.Monster.Alive)
            {
                return;
            }

            if (this.Monster.IsWalking)
            {
                return;
            }

            if (this.currentTarget != null)
            {
                // Old Target out of Range?
                if (!this.currentTarget.Alive
                    || this.currentTarget.IsAtSafezone()
                    || !this.IsTargetInObservers())
                {
                    this.currentTarget = this.SearchNextTarget();
                }
            }
            else
            {
                this.currentTarget = this.SearchNextTarget();
            }

            // no target?
            if (this.currentTarget == null)
            {
                return;
            }

            // Target in Attack Range?
            ushort dist = (ushort)this.currentTarget.GetDistanceTo(this.Npc);
            if (this.Monster.Definition.AttackRange + 1 >= dist)
            {
                this.Monster.Attack(this.currentTarget);  // yes, attack
            }

            // Target in View Range?
            else if (this.Npc.Definition.ViewRange + 1 >= dist)
            {
                // no, walk to the target
                var walkTarget = this.currentTarget.CurrentMap.Terrain.GetRandomCoordinate(this.currentTarget.Position, this.Monster.Definition.AttackRange);
                this.Monster.WalkTo(walkTarget);
            }
            else
            {
                // we move around randomly, so the monster does not look dead when watched from distance.
                this.Monster.RandomMove();
            }
        }
    }
}
