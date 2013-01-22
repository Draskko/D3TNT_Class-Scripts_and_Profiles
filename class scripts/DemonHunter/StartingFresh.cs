/*
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
            base.DoEnter(Entity);
            // We override the default states with our own
            combatState = new DemonHunter.CombatState();
        }

    }

    public class CombatState : Common.CombatState
    {
		// Preparation OPTIONS
		public int DisciplinePct_Preparation = 10;		// Set this to 0 if you do not want it to cast on Disipline Percent
		public int hpPct_Preparation = 40;			// Set this to 0 if you do not want it to cast on HP Percent
		// Shadow Power OPTIONS
		public int hpPct_ShadowPower = 90;
		public static CWSpellTimer ShadowPowerTimer = new CWSpellTimer(5 * 1000);	// 5 * 1000 = 5 seconds
		// Smoke Screen OPTIONS
		public int hpPct_SmokeScreen = 60;
		
        #region ability
        static Dictionary<string, int> abilityInfoDic1 = new Dictionary<string, int>();
        static Dictionary<string, int> abilityInfoDic2 = new Dictionary<string, int>();
        #endregion

        public static bool inited = false;
        public static CWSpellTimer CaltropsTimer = new CWSpellTimer(6 * 1000);
        public static CWSpellTimer moveBackTimer = new CWSpellTimer(3 * 1000);
        const int skillInterval = 50;
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
        public static bool CastDH1TargetSpell(string SpellName, D3Object target)
        {
            if (hasEnoughResource1ForSpell(SpellName))
            {
                return D3Control.CastTargetSpell(SpellName, target);
            }
            return false;
        }
        public static bool NewCastDH1TargetSpell(string SpellName, D3Object target, out bool castSuccessfully)
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

        /// <summary>
        /// This happens when we are being attacked by some mobs or when we
        /// have found something to kill 
        /// </summary>
        protected override void DoExecute(D3Player Entity)
        {
            CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);// in case something is wrong
            bool enemyFound = false;
            while (true)
            {
                if (!D3Control.IsInGame() || D3Control.Player.IsDead || combatThresholdTimer.IsReady)
                    break;
                if (D3Control.Player.HpPct < 50)
                    D3Control.usePotion();
                if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
                {
                    doPulling(D3Control.Player);
                    continue;
                }
                else
                {
                    D3Control.TargetManager.ClearTarget();
                    var enemies = D3Control.TargetManager.GetAroundEnemy(65);
                    enemyFound = false;
                    foreach (D3Unit enemy in enemies)
                    {
                        if (!D3Control.LOS(enemy.Location))
                        {
                            D3Control.TargetManager.SetAttackTarget(enemy);
                            break;
                        }
                    }

                    if (!D3Control.isObjectValid(D3Control.curTarget))
                    {
                        return;
                    }
                    enemyFound = true;
                }
                if (enemyFound)
                    continue;
                if (!D3Control.Player.isInCombat)
                    break;
            }
            return;
        }
        void moveCloser(D3Object target)
        {
            if (target.DistanceFromPlayer > 5)
                D3Control.MoveTo(target, target.DistanceFromPlayer - 5);
        }
        void doPrimary(D3Object target)
        {
            var loc = target.Location;
            bool castSuccessfully = false;
            if (NewCastDH1TargetSpell("Hungering Arrow", target, out castSuccessfully))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                D3Control.Player.Wait(skillInterval);
                return;
            }
            if (NewCastDH1TargetSpell("Bola Shot", target, out castSuccessfully))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                D3Control.Player.Wait(skillInterval);
                return;
            }
            if (NewCastDH1TargetSpell("Entangling Shot", target, out castSuccessfully))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                D3Control.Player.Wait(skillInterval);
                return;
            }
            if (NewCastDH1TargetSpell("Grenades", target, out castSuccessfully))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                D3Control.Player.Wait(skillInterval);
                return;
            }
        }
        void doSecondary(D3Object target)
        {
            if (!D3Control.isObjectValid(target))
                return;
            float d = target.DistanceFromPlayer;
            Vector3D farestspot = D3Control.getSideSpot(D3Control.Player.Location, target.Location, 30);
            if (((D3Unit)target).ID == 0x00035474 || ((D3Unit)target).ID == 0x0002f235)
                farestspot = target.Location;
            bool castSuccessfully;
            //            if (CastDH1TargetSpell("Evasive Fire", target, out castSuccessfully))
            if (NewCastDH1TargetSpell("Evasive Fire", target, out castSuccessfully))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                Thread.Sleep(150);
                //D3Control.Player.Wait(skillInterval);
                if (d < 25)
                {
                    //                    if (CastDH1TargetSpell("Elemental Arrow", target))
                    if (NewCastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
                    {
                        if (!castSuccessfully)
                        {   // cannot cast the spell, such as obstacle, untargetable?
                            moveCloser(target);
                            return;
                        }
                        //D3Control.Player.Wait(skillInterval);
                        Thread.Sleep(150);
                        //                        if (CastDH1TargetSpell("Elemental Arrow", target))
                        if (NewCastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
                        {
                            if (!castSuccessfully)
                            {   // cannot cast the spell, such as obstacle, untargetable?
                                moveCloser(target);
                                return;
                            }
                            //D3Control.Player.Wait(skillInterval);
                            Thread.Sleep(150);
                            return;
                        }
                        return;
                    }
                }
                return;
            }
            else
            {
            }
            //            if (CastDH1TargetSpell("Impale", target))//target.Location))
            if (NewCastDH1TargetSpell("Impale", target, out castSuccessfully))//target.Location))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                D3Control.Player.Wait(skillInterval);
                return;
            }
            //if (CastDH1TargetSpell("Rapid Fire", target))//target.Location))
            if (NewCastDH1TargetSpell("Rapid Fire", target, out castSuccessfully))//target.Location))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                D3Control.Player.Wait(skillInterval);
                return;
            }
            //if (CastDH1TargetSpell("Chakram", target))//target.Location))
            if (NewCastDH1TargetSpell("Chakram", target, out castSuccessfully))//target.Location))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                D3Control.Player.Wait(skillInterval);
                return;
            }
            //            if (CastDH1TargetSpell("Elemental Arrow", target))
            if (NewCastDH1TargetSpell("Elemental Arrow", target, out castSuccessfully))
            {
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
                    moveCloser(target);
                    return;
                }
                //D3Control.Player.Wait(skillInterval);
                Thread.Sleep(50);
                return;
            }
        }
        void HandleAOE()
        {
			//
			D3Object target = D3Control.ClosestEnemy;
			//
            int within50count = D3Control.NearbyEnemyCount(50);
            if (within50count > 1)
            {
				// Multishot
				if (CastDH1Spell("Multishot", target.Location))
				{
					within50count = D3Control.NearbyEnemyCount(50);
                    if (within50count <= 1)
                        return;
				}
				// Rain of Vengeance
                if (CastDH1Spell("Rain of Vengeance", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                    within50count = D3Control.NearbyEnemyCount(50);
                    if (within50count <= 1)
                        return;
                }
			}
			//
            int count = D3Control.NearbyEnemyCount(10);
            if (count > 1)
            {
                if (CastDH1Spell("Fan of Knives", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                    count = D3Control.NearbyEnemyCount(10);
                    if (count <= 1)
                        return;
                }
                if (CastDH1Spell("Strafe", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                    count = D3Control.NearbyEnemyCount(10);
                    if (count <= 1)
                        return;
                }
                if (CastDH1Spell("Spike Trap", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                }
                if (D3Control.canCast("Vault"))
                {
                    Vector3D loc;
                    if (D3Control.isObjectValid(D3Control.curTarget) && D3Control.curTarget.DistanceFromPlayer > 70)
                    {
                        loc = D3Control.getSideSpot(D3Control.Player.Location, 0, 35);  // 0 degree is forward, 90  degree for left side, 180 degree backward, -90 degree right side
                    }
                    else
                        loc = D3Control.getSideSpot(D3Control.Player.Location, 180, 35);
                    CastDH2Spell("Vault", loc);
                    Thread.Sleep(500);
                }
                if (!D3Control.isObjectValid(target))
                    return;
                if (CastDH1Spell("Rapid Fire", target.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                    count = D3Control.NearbyEnemyCount(10);
                    if (count <= 1)
                        return;
                    return;
                }
                if (D3Control.isObjectValid(target))
                    doPrimary(target);
            }
            if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
            {
                D3Object closestTarget = D3Control.ClosestEnemy;
                D3Control.TargetManager.SetAttackTarget((D3Unit)closestTarget);
                D3Control.output("Moving thread working while player is not working? collision? Switch target.");
            }
        }
        public bool moveToStartFight(D3Player entity)
        {
            HandleAOE();
            if (D3Control.Player.HpPct <= 70)
            {
                D3Object healthGlobe = D3Control.ClosestHealthGlobe;
                if (healthGlobe != null)
                {
                    D3Control.MoveTo(healthGlobe, 2.5f);
                    return true;
                }
            }
            if (D3Control.getDoorInWay(D3Control.curTarget) != null)
            {
                D3Control.output("A door inbetween? Try to move closer and open the door.");
                D3Control.MoveTo(D3Control.curTarget, 2.5f);      // a thread doing the move
                return true;
            }

            if (D3Control.curTarget == null)
                return false;
            float d = D3Control.curTarget.DistanceFromPlayer;

            if (d > 45)
            {
                //if (D3Control.LOS(D3Control.curTarget.Location))
                //    D3Control.MoveTo(D3Control.curTarget, 25);      // a thread doing the move
                //else
                D3Control.MoveTo(D3Control.curTarget, 45);      // a thread doing the move
            }
            if (D3Control.isObjectValid(D3Control.curTarget) && D3Control.LOS(D3Control.curTarget.Location) || D3Control.curTarget.Location.Z > D3Control.Player.Location.Z + 10)
            {
                if (!D3Control.isMovingWorking())
                {   // for casters, if LOS(line of sight)
                    if (d <= 5)
                    {
                        D3Control.MoveTo(D3Control.curTarget, 2.5f);      // a thread doing the move
                        return true;
                    }
                    else
                        D3Control.MoveTo(D3Control.curTarget, d - 5f);      // a thread doing the move
                }
                return false;
            }
            return true;
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
			// Rain of Vengance
			if (CastDH1Spell("Rain of Vengeance", D3Control.Player.Location))
			{
				Thread.Sleep(skillInterval);
			}
            return;
        }
        void doPulling(D3Player entity)
        {
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
            checkBuff(entity);
            if (!GlobalBaseBotState.checkBeforePull(entity))
                return;
            D3Unit target = D3Control.curTarget;
            if (target == null || target.IsDead)
                return;
            if (!moveToStartFight(entity))
                return;
            {   // ranged weapon pull 
                if (target.DistanceFromPlayer < 65)
                {
					// Caltrops
					if (CaltropsTimer.IsReady && CastDH2Spell("Caltrops", D3Control.Player.Location))
					{
						CaltropsTimer.Reset();
						Thread.Sleep(skillInterval);
					}	
                    doSecondary(target);
                    if (D3Control.isObjectValid(target))
                        doPrimary(target);
                }
                if (D3Control.isObjectValid(target))
                {
                    //if (target.DistanceFromPlayer < 30 && moveBackTimer.IsReady)
                    //{
                    //    D3Control.ClickMoveTo(D3Control.getSideSpot(D3Control.Player.Location, 180, 8));
                    //    moveBackTimer.Reset();
                    //    Thread.Sleep(150);
                    //}
                }

            }

            if (!D3Control.isMovingWorking())
            {
                D3Control.stopMoving();
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
    /**/
}