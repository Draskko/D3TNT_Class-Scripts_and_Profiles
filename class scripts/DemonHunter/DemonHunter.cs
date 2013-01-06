/*
	Demon Hunter Class Script by ASWeiler
    Last Edited: 12/13/2012
    Thanks To: D3TNT for AOE Avoid and original Core.cs

    This file is part of Astronaut.
    All rights reserved to Astronaut team
    Copyright (C) 2011 Astronaut Team
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

using Astronaut.Bot;
using Astronaut.Common;
using Astronaut.Scripting;
using Astronaut.Scripts.Common;
using Astronaut.D3;
using Astronaut.Monitors;
using System.Threading;
using Astronaut.Scripts;

namespace Astronaut.Scripts.DemonHunter
{
    public class Core : GlobalBaseBotState
    {
        public static int RestHp = 40;
        protected override void DoEnter(D3Player Entity)
        {
			// Set the base of DoEnter based off class
            base.DoEnter(Entity);
            // Init Message
            D3Control.output("Class Script by ASWeiler Loaded");
            // We override the default states with our own
            combatState = new DemonHunter.CombatState();
        }

    }
    public class CombatState : Common.CombatState
    {
        /* OPTIONS */
		const int attackDistance = 40;				// Set how far we look for targets
        public int hpPct_UsePotion = 70;			// When to use HP Potion in Percent!
		public int hpPct_HealthGlobe = 80;			// When we are under the set percent, head for a health globe.
		// Preparation OPTIONS
		public int DisciplinePct_Preparation = 10;		// Set this to 0 if you do not want it to cast on Disipline Percent
		public int hpPct_Preparation = 40;			// Set this to 0 if you do not want it to cast on HP Percent
		// Shadow Power OPTIONS
		public int hpPct_ShadowPower = 90;
		public static CWSpellTimer ShadowPowerTimer = new CWSpellTimer(5 * 1000);	// 5 * 1000 = 5 seconds
		// Smoke Screen OPTIONS
		public int hpPct_SmokeScreen = 60;
		// Vault OPTIONS
		const int escapeDistance = 20;				// The distance we look for targets before Vaulting/Strafing/Running away
		const int escapeAmount = 1;					// The number of targets needed to be in the escapeDistance before Vaulting/Strafing/Running away.	
		static CWSpellTimer VaultTimer = new CWSpellTimer(3 * 1000);
		public bool Vault2Target = true;
		public bool Vault2HPGlobe = true;
		public bool Vault2AvoidAOE = true;
		public bool Vault2Survive = true;
		// Strafe OPTIONS
		public bool Strafe2Target = true;
		public bool Strafe2HPGlobe = true;
		public bool Strafe2AvoidAOE = true;
		public bool Strafe2Survive = true;
		// Misc OPTIONS (Avoid AOE | Focus Pack Leader | Regular and Elite Scan Distances)
		static bool AvoidAoE = true;            // try to avoid AoE (desecrate and middle of arcane beams)
		static bool FocusPackLeader = true;		// Should we go after the pack leader first or just go for the closest mob instead?
		static bool FocusTreasureSeeker = false;// Should we go after the Treasure Seeker (aka Goblin) until it is dead?
        static int MobScanDistance = 40; // scan radius for regular mobs (maximum 100 or about two screens)
		
		/* Timers */
		// Spell Timers
        public static CWSpellTimer CaltropsTimer = new CWSpellTimer(3 * 1000);
		public static CWSpellTimer SpikeTrapTimer = new CWSpellTimer(3 * 1000);
		
		public static CWSpellTimer MarkedForDeathTimer = new CWSpellTimer(10 * 1000);
		// Misc Timers
		static CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);	// Return after 10 seconds regardless
        static CWSpellTimer checkLootTimer = new CWSpellTimer(3 * 1000, false);			// Check for loot every 3 seconds
		static CWSpellTimer checkBuffTimer = new CWSpellTimer(1 * 1000, false);			// Buff Check every 3 seconds
        static CWSpellTimer moveBackTimer = new CWSpellTimer(3 * 1000);					// Move Back Timer
        static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(1 * 1000);			// Check Safe Spot Timer

		
        // Escape AOE stuff
        static Vector3D safeSpot = null, oldSafeSpot = null;
		
        #region ability
        static Dictionary<string, int> abilityInfoDic1 = new Dictionary<string, int>();
        static Dictionary<string, int> abilityInfoDic2 = new Dictionary<string, int>();
        #endregion
		
        public static bool inited = false;
		const int skillInterval = 10;
		public int targetHP = 0;
		
        protected override void DoEnter(D3Player Entity)
        {
            D3Player entity = D3Control.Player;

            if (!inited)
            {
                inited = true;
                setSpirit();
            }
        }
        internal static void setSpirit()
        {
            if (abilityInfoDic1.Count == 0)
            {
                abilityInfoDic1.Add("Rapid Fire", 10);
                abilityInfoDic1.Add("Fan of Knives", 20);
                abilityInfoDic1.Add("Spike Trap", 30);
                abilityInfoDic1.Add("Strafe", 15);
                abilityInfoDic1.Add("Multishot", 40);
                abilityInfoDic1.Add("Cluster Arrow", 50);
                abilityInfoDic1.Add("Impale", 25);
                abilityInfoDic1.Add("Chakram", 10);
                abilityInfoDic1.Add("Elemental Arrow", 10);
            }
            if (abilityInfoDic2.Count == 0)
            {
                abilityInfoDic2.Add("Caltrops", 6);
                abilityInfoDic2.Add("Vault", 8);
                abilityInfoDic2.Add("Shadow Power", 14);
                abilityInfoDic2.Add("Companion", 10);
                abilityInfoDic2.Add("Marked for Death", 3);
            }

        }
        public static bool CastDH1Spell(string SpellName, Vector3D loc)
        {
            if (hasEnoughResource1ForSpell(SpellName))
            {
                return D3Control.CastLocationSpell(SpellName, loc, true);
            }
            return false;
        }
        public static bool CastDH1TargetSpell(string SpellName, D3Object target, out bool castSuccessfully)
        {
            castSuccessfully = false;
            if (hasEnoughResource1ForSpell(SpellName))
            {
                return D3Control.NewCastTargetSpell(SpellName, target, out castSuccessfully);
                
            }
            return false;
        }
        public static bool CastDH2Spell(string SpellName, Vector3D loc)
        {
            if (hasEnoughResource2ForSpell(SpellName))
            {
                return D3Control.CastLocationSpell(SpellName, loc, true);
            }
            return false;
        }
        public static bool hasEnoughResource1ForSpell(string SpellName)
        {
            if (abilityInfoDic1.ContainsKey(SpellName))
            {
                if (D3Control.Player.Hatred < abilityInfoDic1[SpellName])
                    return false;
                else
                    return true;
            }
            else
                return true;
        }
        public static bool hasEnoughResource2ForSpell(string SpellName)
        {
            if (abilityInfoDic2.ContainsKey(SpellName))
            {
                if (D3Control.Player.Disc < abilityInfoDic2[SpellName])
                    return false;
                else
                    return true;
            }
            else
                return true;
        }
        //
        internal static bool isWithinAttackDistance(D3Unit target)
        {
			// First check if we are close enough to an special target. If we are return true;
			// 	   	  170324 & 139454 is Act 1 Tree Normal
			//				   218947 is Elite Worm in Act 2
			//				   221291 is Act 2 Jumpers from wall in Sundered Canyon
			//				   3384 is the birds in Act 2
			//				   3037 is the summoning thing in Act 2
			//				   3349 IS BELIAL
            //				   193077 is Heart of Sin
            //                 230725 are the beasts on the side of the Rakkis Crossing bridge
            //                 96192  is Siegebreaker
            //                 89690  is Azmodan
			//				   95250  is Queen before Core of Arreat
			//				   137139 is Spiders to Queen
			//				   203048 is Worm in Act 3 - Arreat Creater
            if (((int)target.DistanceFromPlayer <= attackDistance) || 
                ((target.ID == 193077) && ((int)target.DistanceFromPlayer <= 30)) || 
                ((target.ID == 230725) && ((int)target.DistanceFromPlayer <= 10)) ||
                ((target.ID == 96192)  && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 220313) && ((int)target.DistanceFromPlayer <= 40)) ||
				((target.ID == 144315) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 5581) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 495) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 5188) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 5191) && ((int)target.DistanceFromPlayer <= 40)) ||
				((target.ID == 218947) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 170324) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 139454) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 221291) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 3384) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 3037) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 95250) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 137139) && ((int)target.DistanceFromPlayer <= 20)) ||
                ((target.ID == 89690)  && ((int)target.DistanceFromPlayer <= 45))				
				)
                    return true;

            return false;
        }
		
        bool pickTarget()
        {
            int enemyCount = 0;
            bool found = false;
            float closestEliteDistance = 40, closestMobDistance = 40;
            D3Unit closestElite = null, closestMob = null, u;

            D3Control.TargetManager.ClearTarget();

            // 65 is about one full screen length
            var mobs = D3Control.TargetManager.GetAroundEnemy(100);
            foreach (D3Unit mob in mobs) 
            {
                enemyCount++;
                if (!mob.IsDead && !D3Control.LOS(mob.Location) && !found) 
                {
                    if(mob.DistanceFromPlayer < closestMobDistance) 
                    {
                        closestMobDistance = mob.DistanceFromPlayer;
                        closestMob = mob;
                    }

                    // MLevel 2 is a leader (with minions)
                    if ((mob.MLevel == 2) && FocusPackLeader)
                    {
                        D3Control.output("Found Pack Leader ID: " + mob.ID + " Dist: " + (int)mob.DistanceFromPlayer + " ML: " + mob.MLevel);
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;
                    }
						
					// TS
                    if ((mob.ID == 5984 || mob.ID == 5985 || mob.ID == 5987 || mob.ID == 5988) && FocusTreasureSeeker)
                    {
                        D3Control.output("Found Treasure Seeker!! Distance: " + (int)mob.DistanceFromPlayer);
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;
                    }
                }
            }

            //D3Control.output("DoExecute Cnt: " + enemyCount + " cElite:" + (int)closestEliteDistance + " cMob: " + (int)closestMobDistance);

            // kill all trash mobs within RegularMobScanDistance and elites within EliteMobScanDistance
            if ((closestMobDistance <= MobScanDistance) && (closestMob != null)) {
                D3Control.TargetManager.SetAttackTarget(closestMob);
                found = true;
            }

            if (!found)
            {
                return false;
            }
            else
            {
                u = D3Control.curTarget;
                //D3Control.output("Target ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                return true;
            }
        }

        /// <summary>
        /// This happens when we are being attacked by some mobs or when we
        /// have found something to kill 
        /// </summary>
        /// </summary>
		
        protected override void DoExecute(D3Player Entity)
        {
			// Check if we need to re-cast buffs
			checkBuff(Entity);
            // return after 10 seconds regardless
            CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);
			//
            D3Unit originalTarget, u;
            originalTarget = D3Control.curTarget;
			//
            if (!pickTarget())
            {
                // if we don't pick a target we must use the default provided else we will
                // get into a deadlock
                u = originalTarget;
                D3Control.output("DoEx Orig ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                D3Control.TargetManager.SetAttackTarget(originalTarget);
            }
            // loop until we run out of targets or the timer expires
            while (true)
            {
				Thread.Sleep(50);
				// If the combat timer expires, break out of DoExecute
                if (combatThresholdTimer.IsReady)
				{
                   D3Control.output("Combat Timer Expired!");
				   break;
				}
				// If we are not in game, or are dead, or the combat timer reset, break out of DoExecute
                if (!D3Control.IsInGame() || D3Control.Player.IsDead || combatThresholdTimer.IsReady)
                    break;
				// Check if we should look for AOE to avoid and avoid it if we are set to and we are in combat.
				if (AvoidAoE && checkSafeSpotTimer.IsReady && D3Control.Player.isInCombat)
				{
					// Reset safe spot checking timer
					checkSafeSpotTimer.Reset();
					// Get AOE info
					var dTargetInfo = D3Control.getDangourousTargetInfo();
					// Set the safe spot to an known safe spot based off the AOE info
					safeSpot = D3Control.getSafePoint(dTargetInfo);
					// If the oldSafeSpot is not the same as the current SafeSpot then get out of the AOE
					if (oldSafeSpot != safeSpot)
					{
						// If the moveBackTimer is ready to use again, avoid the AOE
						if (moveBackTimer.IsReady)
						{
							// Reset the timer
							moveBackTimer.Reset();
							// Avoid the AOE by clicking to the safe spot to move out of the AOE
							D3Control.output("Try to avoid AoE!");
							D3Control.ClickMoveTo(safeSpot);
							Thread.Sleep(750);
						}
						// Reset
						oldSafeSpot = safeSpot;
					}
				}
				// Update dungeon info
                D3Control.updateDungoneInfo();
                // Pickup Health Globe if satisfied condition -- KiDD
                if (D3Control.Player.HpPct <= hpPct_HealthGlobe)
                {
                    D3Object healthGlobe = D3Control.ClosestHealthGlobe;
                    if (healthGlobe != null)
                    {   
                        // If health globe exist, go get it
                        D3Control.MoveTo(healthGlobe, 2.5f);
                    }
                    else
                    {
                        // Check if we need to use a potion
                        if (D3Control.Player.HpPct < hpPct_UsePotion)
                            D3Control.usePotion();
                    }
                    healthGlobe = null;
                }
				// Check if we need to re-cast buffs
				checkBuff(Entity);
				// Do Pulling if we got a target to kill!
                if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
                {
                    D3Control.TargetManager.handleSpecialFight();
                    doPulling(D3Control.Player);
                    continue;
                }
                else
                {
                    if (!pickTarget())
                    {
                        D3Control.output("No target found, returning from DoExecute.");
                        break;
                    }
                }
            }
		}
        void HandleAOE()
        {
			// Check if there are enough mobs within the escapeDistance to vaildate running back.
			var escapemobs = D3Control.TargetManager.GetAroundEnemy(escapeDistance).Count;
			if (escapemobs > escapeAmount)
			{
				// Vault
				if (D3Control.canCast("Vault") && Vault2Survive && VaultTimer.IsReady)
				{
					Vector3D loc;
					loc = D3Control.getSideSpot(D3Control.Player.Location, 180, 35);
					D3Control.output("Vaulting Back!");
					CastDH2Spell("Vault", loc);
					VaultTimer.Reset();
					Thread.Sleep(skillInterval);
				}
				// Strafe
				else if (D3Control.canCast("Strafe") && Strafe2Survive)
				{
					D3Control.output("Strafe");
					Vector3D loc;
					loc = D3Control.getSideSpot(D3Control.Player.Location, 180, 35);
					D3Control.output("Strafing Back!");
					CastDH1Spell("Strafe", loc);
					Thread.Sleep(skillInterval);
				}
				else
				{
					Vector3D loc;
					loc = D3Control.getSideSpot(D3Control.Player.Location, 180, 35);
					D3Control.output("Running Back!");
					D3Control.ClickMoveTo(loc);
				}
			}
			// Check for more than one mob that are within 12 so we can use spells like Caltrops or Fan of Knives
			var within12mobs = D3Control.TargetManager.GetAroundEnemy(12).Count;
			if (within12mobs > 1)
			{
				// Fan of Knives
				if (CastDH1Spell("Fan of Knives", D3Control.Player.Location))
				{
					Thread.Sleep(skillInterval);
				}				
			}
			// Check for more than one mob within attack distance to cast spells like Multishot and Spike Trap
			var withinattackdistancemobs = D3Control.TargetManager.GetAroundEnemy(attackDistance).Count;
			if (withinattackdistancemobs > 1)
			{
				// Caltrops
				if (CaltropsTimer.IsReady && CastDH2Spell("Caltrops", D3Control.Player.Location))
				{
					CaltropsTimer.Reset();
					Thread.Sleep(skillInterval);
				}			
			}
			return;
        }
        public static void checkBuff(D3Player entity)
        {
            if (D3Control.canCast("Companion") && !D3Control.HasDHComanion())
            {
                if (CastDH2Spell("Companion", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                }
            }
            if (D3Control.canCast("Sentry") && !D3Control.HasDHSentry())
            {
                if (CastDH2Spell("Sentry", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                }
            }
            if ((D3Control.Player.Disc < DisciplinePct_Preparation || D3Control.Player.HpPct < hpPct_Preparation) && D3Control.CastLocationSpell("Preparation",D3Control.Player.Location, true))
            {
                Thread.Sleep(skillInterval);
            }
            if (D3Control.Player.HpPct <= hpPct_SmokeScreen && CastDH2Spell("Smoke Screen", D3Control.Player.Location))
            {
                Thread.Sleep(skillInterval);
            }
            if (D3Control.Player.HpPct <= hpPct_ShadowPower && ShadowPowerTimer.IsReady && CastDH2Spell("Shadow Power", D3Control.Player.Location))
            {
                Thread.Sleep(skillInterval);
				ShadowPowerTimer.Reset();
            }
			// Rain of Vengance
			if (CastDH1Spell("Rain of Vengeance", D3Control.Player.Location))
			{
				Thread.Sleep(skillInterval);
			}
            return;
        }
        void doPulling(D3Player entity)
        {
					// Check if we should look for AOE to avoid and avoid it if we are set to and we are in combat.
					if (AvoidAoE && checkSafeSpotTimer.IsReady && D3Control.Player.isInCombat)
					{
						// Reset safe spot checking timer
						checkSafeSpotTimer.Reset();
						// Get AOE info
						var dTargetInfo = D3Control.getDangourousTargetInfo();
						// Set the safe spot to an known safe spot based off the AOE info
						safeSpot = D3Control.getSafePoint(dTargetInfo);
						// If the oldSafeSpot is not the same as the current SafeSpot then get out of the AOE
						if (oldSafeSpot != safeSpot)
						{
							// If the moveBackTimer is ready to use again, avoid the AOE
							if (moveBackTimer.IsReady)
							{
								// Reset the timer
								moveBackTimer.Reset();
								// Avoid the AOE by clicking to the safe spot to move out of the AOE
								D3Control.output("Try to avoid AoE!");
								D3Control.ClickMoveTo(safeSpot);
								Thread.Sleep(750);
							}
							// Reset
							oldSafeSpot = safeSpot;
						}
					}
			//
			bool castSuccessfully = false;
			// Set target variable for easy use of current target
			D3Unit target = D3Control.curTarget;
			// Get the distance to the target from the player
			float d = target.DistanceFromPlayer;
			// BOT STATE BEFORE PULL
            if (!GlobalBaseBotState.checkBeforePull(entity))
                return;
			// Start with AOE spells
			HandleAOE();
			// Check if we need to re-cast buffs
			checkBuff(entity);
			// Pickup Health Globe if we should and can 
			if (D3Control.Player.HpPct <= hpPct_HealthGlobe)
			{
				D3Object healthGlobe = D3Control.ClosestHealthGlobe;
				if (healthGlobe != null)
				{   
					// If health globe exist, go get it
					D3Control.MoveTo(healthGlobe, 2.5f);
				}
				healthGlobe = null;
			}
			// Check if we need to use a potion
			if (D3Control.Player.HpPct < hpPct_UsePotion)
				D3Control.usePotion();
			// Make sure the target is still valid
			if (D3Control.LOS(target.Location) || D3Control.curTarget.IsDead || !D3Control.isObjectValid(target))
				return;
			// If we are trying to move forward, but we arn't, attack in front of ourselves to clear a path
			if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
			{
				/*
					SECONDARY SPELLS
				*/
				// Rapid Fire
				if (D3Control.canCast("Rapid Fire") && CastDH1TargetSpell("Rapid Fire", target, out castSuccessfully))
				{
					Thread.Sleep(50);
					return;
				}
				// Multishot
				if (D3Control.canCast("Multishot"))
				{
					if (CastDH1TargetSpell("Multishot", target, out castSuccessfully))
					{
						Thread.Sleep(skillInterval);
						return;
					}
				}
				// Spike Trap
				if (SpikeTrapTimer.IsReady && CastDH1TargetSpell("Spike Trap", target, out castSuccessfully))
				{
					SpikeTrapTimer.Reset();
					return;
				}
				// Evasive Fire
				if (CastDH1TargetSpell("Evasive Fire", target, out castSuccessfully))
				{
					Thread.Sleep(150);
					if (d < 25)
					{
						if (CastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
						{
							Thread.Sleep(150);
							if (CastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
							{
								Thread.Sleep(150);
							}
						}
					}
					return;
				}
				// Marked for Death
				if (MarkedForDeathTimer.IsReady && CastDH2Spell("Marked for Death", target.Location))
				{
					MarkedForDeathTimer.Reset();
					return;
				}
				// Impale
				if (CastDH1TargetSpell("Impale", target, out castSuccessfully))
				{
					return;
				}
				// Chakram
				if (CastDH1TargetSpell("Chakram", target, out castSuccessfully))
				{
					return;
				}
				// Cluster Arrow
				if (D3Control.curTarget.IsElite && D3Control.canCast("Cluster Arrow"))
				{
					CastDH1TargetSpell("Cluster Arrow", target, out castSuccessfully);
					return;
				}
				// Elemental Arrow
				if (CastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
				{
					return;
				}
				// Make sure the target is still valid
				if (D3Control.LOS(target.Location) || D3Control.curTarget.IsDead || !D3Control.isObjectValid(target))
					return;
				/*
					PRIMARY SPELLS
				*/
				// Hungring Arrow
				if (CastDH1TargetSpell("Hungering Arrow", target, out castSuccessfully))
				{
					return;
				}
				// Bola Shot
				if (CastDH1TargetSpell("Bola Shot", target, out castSuccessfully))
				{
					return;
				}
				// Entangling Shot
				if (CastDH1TargetSpell("Entangling Shot", target, out castSuccessfully))
				{
					return;
				}
				// Grenades
				if (CastDH1TargetSpell("Grenades", target, out castSuccessfully))
				{
					return;
				}
			}
            // nothing in range or no LOS to target so lets move closer
            if (!isWithinAttackDistance(target) || D3Control.LOS(target.Location))
            {
				//
                if (d <= 10 && D3Control.LOS(target.Location))
				{
					D3Control.MoveTo(target, d - 2.5f);
					return;
				}
				else if (D3Control.LOS(target.Location) || !isWithinAttackDistance(target))
				{
					if (D3Control.LOS(target.Location) && !isWithinAttackDistance(target))
					{
						D3Control.output("Not in line of sight and outside attack distance of"+target.ID+" ("+d+"). Moving in closer.");
					}
					else if (D3Control.LOS(target.Location) && isWithinAttackDistance(target))
					{
						D3Control.output("Not in line of sight of"+target.ID+" ("+d+"). Moving in closer.");
					}
					D3Control.MoveTo(target, d-10);
					return;
				}
            }
			D3Control.output("Attacking Target "+target.ID+" HP:"+(int)target.Hp);
			// Make sure the target is still valid
			if (D3Control.LOS(target.Location) || D3Control.curTarget.IsDead || !D3Control.isObjectValid(target))
				return;
			/*
				SECONDARY SPELLS
			*/
			// Rapid Fire
			if (D3Control.canCast("Rapid Fire") && CastDH1TargetSpell("Rapid Fire", target, out castSuccessfully))
			{
				Thread.Sleep(50);
				return;
			}
			// Multishot
			if (D3Control.canCast("Multishot"))
			{
				if (CastDH1TargetSpell("Multishot", target, out castSuccessfully))
				{
					Thread.Sleep(skillInterval);
					return;
				}
			}
			// Spike Trap
			if (SpikeTrapTimer.IsReady && CastDH1TargetSpell("Spike Trap", target, out castSuccessfully))
			{
				SpikeTrapTimer.Reset();
				return;
			}
			// Evasive Fire
			if (CastDH1TargetSpell("Evasive Fire", target, out castSuccessfully))
			{
				Thread.Sleep(150);
				if (d < 25)
				{
					if (CastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
					{
						Thread.Sleep(150);
						if (CastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
						{
							Thread.Sleep(150);
						}
					}
				}
				return;
			}
			// Marked for Death
			if (MarkedForDeathTimer.IsReady && CastDH2Spell("Marked for Death", target.Location))
			{
				MarkedForDeathTimer.Reset();
				return;
			}
			// Impale
			if (CastDH1TargetSpell("Impale", target, out castSuccessfully))
			{
				return;
			}
			// Chakram
			if (CastDH1TargetSpell("Chakram", target, out castSuccessfully))
			{
				return;
			}
			// Cluster Arrow
			if (D3Control.curTarget.IsElite && D3Control.canCast("Cluster Arrow"))
			{
				CastDH1TargetSpell("Cluster Arrow", target, out castSuccessfully);
				return;
			}
			// Elemental Arrow
			if (CastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
			{
				return;
			}
			// Make sure the target is still valid
			if (D3Control.LOS(target.Location) || D3Control.curTarget.IsDead || !D3Control.isObjectValid(target))
				return;
			/*
				PRIMARY SPELLS
			*/
			// Hungring Arrow
			if (CastDH1TargetSpell("Hungering Arrow", target, out castSuccessfully))
			{
				return;
			}
			// Bola Shot
			if (CastDH1TargetSpell("Bola Shot", target, out castSuccessfully))
			{
				return;
			}
			// Entangling Shot
			if (CastDH1TargetSpell("Entangling Shot", target, out castSuccessfully))
			{
				return;
			}
			// Grenades
			if (CastDH1TargetSpell("Grenades", target, out castSuccessfully))
			{
				return;
			}
        }
        //
        void moveback(int length)
        {
            if (moveBackTimer.IsReady)
            {
                if (D3Control.curTarget.DistanceFromPlayer < 40)
                {
                    moveBackTimer.Reset();
					//
					Vector3D loc = D3Control.getSideSpot(D3Control.Player.Location, 180, length);
					if (D3Control.canCast("Vault") && VaultTimer.IsReady)
					{
						CastDH1Spell("Vault", loc);
						VaultTimer.Reset();
						Thread.Sleep(skillInterval);
					}
					// Try to Strafe to safespot
					else if (D3Control.canCast("Strafe"))
					{
						CastDH1Spell("Strafe", loc);
						Thread.Sleep(skillInterval);
					}
					// Walk to it instead
					else
					{
						D3Control.ClickMoveTo(loc);
					}
					//
                    while (D3Control.Player.isMovingForward)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
        }
		
        protected override void DoExit(D3Player entity)
        {
            //on exit, if there is a previous state, go back to it
            if (PreviousState != null)
            {
                CallChangeStateEvent(entity, PreviousState, false, false);
            }
        }

        protected override void DoFinish(D3Player entity)
        {

        }
    }
}