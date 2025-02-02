﻿using SaveOurShip2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorld
{
    public class CompBuildingConsciousness : ThingComp
    {
        public Pawn Consciousness;
        public Color HologramColor;
        public CompProperties_BuildingConsciousness Props => (CompProperties_BuildingConsciousness)props;
        public int HologramRespawnTick = 0;
        public string AIName = "Unnamed AI";
        public Thing WhichPawn;
        public Thing RezPlz;

        static Color[] colors = new Color[] { new Color(0, 1f, 0.5f, 0.8f), new Color(0, 0.5f, 1f, 0.8f), new Color(1f, 0.25f, 0.25f, 0.8f), new Color(1f, 0.8f, 0, 0.8f), new Color(0.75f,0,1f,0.8f), new Color(1f,0.5f,0,0.8f), new Color (0.1f,0.1f,0.1f,0.8f), new Color (0.9f,0.9f,0.9f,0.8f)};
        static string[] colorNames = new string[] { "Green", "Blue", "Red", "Yellow", "Purple", "Orange", "Black", "White"};
        public static List<ThingDef> drugs = new List<ThingDef>();
        public static List<ThingDef> apparel = new List<ThingDef>();

        bool SavedDeep = false;

        CompPowerTrader compPower;

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (Consciousness!=null && !Consciousness.Spawned && !Find.WorldPawns.Contains(Consciousness) && !Consciousness.InContainerEnclosed && Consciousness.CarriedBy==null)
                {
                    SavedDeep = true;
                    Scribe_Values.Look<bool>(ref SavedDeep, "SavedDeep");
                    Scribe_Deep.Look<Pawn>(ref Consciousness, "Consciousness");
                }
                else
                {
                    SavedDeep = false;
                    Scribe_Values.Look<bool>(ref SavedDeep, "SavedDeep");
                    Scribe_References.Look<Pawn>(ref Consciousness, "Consciousness");
                }
            }
            else
            {
                Scribe_Values.Look<bool>(ref SavedDeep, "SavedDeep");
                if (SavedDeep)
                {
                    foreach (Pawn p in PawnsFinder.All_AliveOrDead)
                    {
                        if (ShipInteriorMod2.IsHologram(p) && p.health.hediffSet.GetFirstHediff<HediffPawnIsHologram>().consciousnessSource == parent)
                        {
                            Consciousness = p;
                            Log.Message("Fixed duplicate consciousness bug");
                            SavedDeep = false;
                        }
                    }
                    if(SavedDeep)
                        Scribe_Deep.Look<Pawn>(ref Consciousness, "Consciousness");
                }
                else
                    Scribe_References.Look<Pawn>(ref Consciousness, "Consciousness");
            }
            Scribe_Values.Look<Color>(ref HologramColor, "HologramColor");
            Scribe_Values.Look<int>(ref HologramRespawnTick, "HologramRespawnTick");
            Scribe_Values.Look<string>(ref AIName, "AIName");
            Scribe_References.Look<Thing>(ref WhichPawn, "PawnToUse");
            Scribe_References.Look<Thing>(ref RezPlz, "Resurrector");
        }
        public void GenerateAIPawn()
        {
            PawnGenerationRequest req = new PawnGenerationRequest(PawnKindDef.Named("SoSHologram"), Faction.OfPlayer, PawnGenerationContext.NonPlayer, -1, false, true, false, false, false, 0, true, false, false, false, false, false, false, false, true, 0, 0, forceNoIdeo: true, forbidAnyTitle: true);
            Pawn p = PawnGenerator.GeneratePawn(req);
            p.story.Childhood = ShipInteriorMod2.hologramBackstory;
            p.Name = new NameTriple("", AIName, "");
            p.Position = parent.Position;
            p.relations = new Pawn_RelationsTracker(p);
            p.interactions = new Pawn_InteractionsTracker(p);
            while (p.story.traits.allTraits.Count > 0)
            {
                p.story.traits.allTraits.RemoveLast();
            }
            Consciousness = p;
            foreach(SkillRecord skill in Consciousness.skills.skills)
            {
                skill.passion = Passion.Minor;
                skill.levelInt = 5;
                bool dummy=skill.TotallyDisabled;
            }
            Consciousness.skills.Notify_SkillDisablesChanged();
            if (ModsConfig.IdeologyActive)
                Consciousness.ideo.SetIdeo(Faction.OfPlayer.ideos.PrimaryIdeo);
            SetupConsciousness();
        }

        void SetupConsciousness(List<Apparel> pawnApparel = null, bool graphicsDirty=true)
        {
            List<Hediff> hediffs = new List<Hediff>();
            foreach(Hediff hediff in Consciousness.health.hediffSet.hediffs)
            {
                hediffs.Add(hediff);
            }
            foreach(Hediff hediff in hediffs)
            {
                if (hediff.def.spawnThingOnRemoved != null)
                {
                    Consciousness.health.RemoveHediff(hediff);
                    GenPlace.TryPlaceThing(ThingMaker.MakeThing(hediff.def.spawnThingOnRemoved), Consciousness.Position, parent.Map, ThingPlaceMode.Near);
                }
                else
                {
                    if (hediff.Part == null || (hediff.Part.def.tags != null && hediff.Part.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource)))
                    {
                        if (Props.healOnMerge && (hediff.def.isBad || hediff is Hediff_Addiction))
                            Consciousness.health.RemoveHediff(hediff);
                    }
                    else
                    {
                        Consciousness.health.RemoveHediff(hediff);
                    }
                }
            }
            HediffPawnIsHologram hologramHediff=(HediffPawnIsHologram)Consciousness.health.AddHediff(Props.holoHediff);
            hologramHediff.consciousnessSource = (Building)parent;
            Consciousness.needs.AddOrRemoveNeedsAsAppropriate();
            if (Props.canMergeAI && Props.canMergeHuman) //Archotech
            {
                HologramColor = colors[0];
                foreach (SkillRecord skill in Consciousness.skills.skills)
                {
                    if (skill.levelInt < 10)
                        skill.levelInt = 10;
                }
                if(ModsConfig.RoyaltyActive)
                {
                    while (Consciousness.GetPsylinkLevel() < 6)
                        Consciousness.ChangePsylinkLevel(1);
                    foreach (AbilityDef ability in DefDatabase<AbilityDef>.AllDefs)
                        Consciousness.abilities.GainAbility(ability);
                }
            }
            else if (Props.canMergeHuman) //Avatar
                HologramColor = colors[2];
            else //AI
                HologramColor = colors[1];
            Consciousness.story.HairColor = HologramColor;
            Consciousness.story.skinColorOverride = HologramColor;
            if(graphicsDirty)
                Consciousness.Drawer.renderer.graphics.SetAllGraphicsDirty();
            HologramDestroyed(false);
            Consciousness.kindDef = PawnKindDef.Named("SoSHologram");
            Consciousness.def = ThingDef.Named("SoSHologramRace");
            typeof(Pawn_AgeTracker).GetMethod("RecalculateLifeStageIndex", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Consciousness.ageTracker, new object[] { });
            if(pawnApparel!=null)
            {
                foreach(Apparel app in pawnApparel)
                {
                    if (app.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead) || app.def.apparel.layers.Contains(ApparelLayerDefOf.Shell) || (app.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin) && (app.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs) || app.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))))
                        AddHolographicApparel(app.def);
                }
            }
        }

        public void HologramDestroyed(bool decohere, bool goneForGood = false)
        {
            if (Consciousness == null)
                return;
            Thing thing = null;
            if(Consciousness.carryTracker.CarriedThing!=null)
                Consciousness.carryTracker.TryDropCarriedThing(Consciousness.Position, ThingPlaceMode.Near, out thing);
            if(Consciousness.Spawned)
                Consciousness.inventory.DropAllNearPawn(Consciousness.Position);
            if (decohere)
            {
                HologramRespawnTick = Find.TickManager.TicksGame + 60000;
                GenExplosion.DoExplosion(Consciousness.Position, Consciousness.Map, 4.9f, DamageDefOf.EMP, Consciousness);
                if(goneForGood && !Consciousness.Dead)
                    Consciousness.Kill(null);
            }
            if (Consciousness.Spawned)
                Consciousness.DeSpawn();
            if(Consciousness.InContainerEnclosed)
            {
                Consciousness.ParentHolder.GetDirectlyHeldThings().Remove(Consciousness);
            }
            if (Consciousness.CarriedBy != null)
                Consciousness.CarriedBy.carryTracker.DestroyCarriedThing();
            Thing relay = Consciousness.health.hediffSet.GetFirstHediff<HediffPawnIsHologram>().relay;
            Consciousness.health.hediffSet.GetFirstHediff<HediffPawnIsHologram>().relay = null;
            if(relay!=null)
                relay.TryGetComp<CompHologramRelay>().StopRelaying(false);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if(mode==DestroyMode.Deconstruct || mode==DestroyMode.KillFinalize)
                HologramDestroyed(true, true);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            List<Gizmo> gizmos = new List<Gizmo>();
            gizmos.AddRange(base.CompGetGizmosExtra());
            if (ResearchProjectDef.Named("ShipAIHologram").IsFinished||Props.canMergeHuman)
            {
                if (parent.Faction == Faction.OfPlayer && compPower.PowerOn)
                {
                    if (Consciousness != null)
                    {
                        if (!Consciousness.Spawned && !Consciousness.InContainerEnclosed && Consciousness.CarriedBy==null)
                        {
                            Command_Action spawn = new Command_Action
                            {
                                action = delegate
                                {
                                    SpawnHologram();
                                },
                                defaultLabel = "SoSSpawnHologram".Translate(),
                                defaultDesc = "SoSSpawnHologramDesc".Translate(),
                                icon = ContentFinder<Texture2D>.Get("UI/SpawnHologram", true)
                            };
                            if(Find.TickManager.TicksGame<this.HologramRespawnTick)
                            {
                                spawn.disabled = true;
                                spawn.disabledReason = "SoSSpawnHologramDelay".Translate(GenDate.ToStringTicksToPeriod(this.HologramRespawnTick-Find.TickManager.TicksGame));
                            }
                            gizmos.Add(spawn);
                        }
                        else
                        {
                            gizmos.Add(new Command_Action
                            {
                                action = delegate
                                {
                                    HologramDestroyed(false);
                                },
                                defaultLabel = "SoSDespawnHologram".Translate(),
                                defaultDesc = "SoSDespawnHologramDesc".Translate(),
                                icon = ContentFinder<Texture2D>.Get("UI/DespawnHologram", true)
                            });
                        }
                    }
                    else if (!Props.canMergeAI && !Props.canMergeHuman)
                    {
                        gizmos.Add(new Command_Action
                        {
                            action = delegate
                            {
                                GenerateAIPawn();
                                SpawnHologram();
                            },
                            defaultLabel = "SoSSpawnHologram".Translate(),
                            defaultDesc = "SoSSpawnHologramDesc".Translate(),
                            icon = ContentFinder<Texture2D>.Get("UI/SpawnHologram", true)
                        });
                    }
                }
                if (Consciousness != null)
                {
                    gizmos.Add(new Command_Action
                    {
                        action = delegate
                        {
                            List<FloatMenuOption> options = new List<FloatMenuOption>();
                            foreach (string name in colorNames)
                            {
                                options.Add(new FloatMenuOption(name, delegate
                                {
                                    this.HologramColor = colors[colorNames.FirstIndexOf(colorname => colorname == name)];
                                    Consciousness.story.HairColor = HologramColor;
                                    Consciousness.story.skinColorOverride = HologramColor;
                                    Consciousness.Drawer.renderer.graphics.SetAllGraphicsDirty();
                                    PortraitsCache.SetDirty(Consciousness);
                                }));
                            }
                            if(options.Count>0)
                                Find.WindowStack.Add(new FloatMenu(options));
                        },
                        defaultLabel = "SoSHologramColor".Translate(),
                        defaultDesc = "SoSHologramColorDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("RoughAlphaAdd", true)
                    });
                    gizmos.Add(new Command_Action
                    {
                        action = delegate
                        {
                            List<FloatMenuOption> options = new List<FloatMenuOption>();
                            foreach (ThingDef drug in drugs)
                            {
                                options.Add(new FloatMenuOption(drug.label, delegate
                                {
                                    ThingMaker.MakeThing(drug).Ingested(Consciousness, 0);
                                    Messages.Message("SoSHologramDrugTaken".Translate(Consciousness, drug.label), MessageTypeDefOf.PositiveEvent);
                                }));
                            }
                            if (options.Count > 0)
                                Find.WindowStack.Add(new FloatMenu(options));
                        },
                        defaultLabel = "SoSHologramDrug".Translate(),
                        defaultDesc = "SoSHologramDrugDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("Things/Item/Resource/PlantFoodRaw/SmokeleafLeaves", true)
                    });
                    gizmos.Add(new Command_Action
                    {
                        action = delegate
                        {
                            List<FloatMenuOption> options = new List<FloatMenuOption>();
                            foreach (ThingDef app in apparel)
                            {
                                options.Add(new FloatMenuOption(app.label, delegate
                                {
                                    AddHolographicApparel(app);
                                },app));
                            }
                            if (options.Count > 0)
                                Find.WindowStack.Add(new FloatMenu(options));
                        },
                        defaultLabel = "SoSHologramApparel".Translate(),
                        defaultDesc = "SoSHologramApparelDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("Things/Pawn/Humanlike/Apparel/Pants/Pants", true)
                    });
                }
            }

            if (!this.parent.Map.GetComponent<ShipHeatMapComp>().InCombat && ((!Props.canMergeAI && !Props.canMergeHuman) || Props.healOnMerge)) //AI or archotech
            {
                gizmos.Add(new Command_Action
                {
                    action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_NameAI(this));
                    },
                    defaultLabel = TranslatorFormattedStringExtensions.Translate("ShipInsideRenameAI"),
                    defaultDesc = TranslatorFormattedStringExtensions.Translate("ShipInsideRenameAIDesc"),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/RenameZone")
                });
            }

            if(Props.canMergeHuman && Consciousness==null)
            {
                Command_Action installBrain=new Command_Action
                {
                    action=delegate
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        if (!Props.mustBeDead)
                        {
                            foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned)
                            {
                                if (p.Faction == Faction.OfPlayer && !p.Dead && p.IsColonist)
                                {
                                    options.Add(new FloatMenuOption(p.Label, delegate
                                    {
                                        WhichPawn = p;
                                    }));
                                }
                            }
                        }
                        else
                        {
                            foreach(Corpse c in parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                            {
                                Pawn p = c.InnerPawn;
                                var brainDef = p.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.ConsciousnessSource).FirstOrDefault()?.def;
                                if (p.Faction == Faction.OfPlayer && p.IsColonist && p.GetRotStage() == RotStage.Fresh && p.health.hediffSet.GetNotMissingParts().Any(part => part.def == brainDef))
                                {
                                    options.Add(new FloatMenuOption(p.Label, delegate
                                    {
                                        WhichPawn = p;
                                    }));
                                }
                            }
                        }
                        if (options.Count > 0)
                            Find.WindowStack.Add(new FloatMenu(options));
                    },
                    defaultLabel = WhichPawn==null ? TranslatorFormattedStringExtensions.Translate("ShipInsideInstallConsciousness") : TranslatorFormattedStringExtensions.Translate("ShipInsideInstallConsciousnessThisOne",WhichPawn),
                    defaultDesc = TranslatorFormattedStringExtensions.Translate("ShipInsideInstallConsciousnessDesc"),
                    icon = Props.healOnMerge ? ContentFinder<Texture2D>.Get("UI/ArchoTechUpload") : ContentFinder<Texture2D>.Get("UI/InstallBrain")
                };
                installBrain.disabled = true;
                if (!Props.mustBeDead)
                {
                    foreach (Pawn p in parent.Map.mapPawns.AllPawnsSpawned)
                    {
                        if (p.Faction == Faction.OfPlayer && !p.Dead && p.IsColonist)
                            installBrain.disabled = false;
                    }
                }
                else
                {
                    foreach (Corpse c in parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                    {
                        Pawn p = c.InnerPawn;
                        if (p.Faction == Faction.OfPlayer && p.IsColonist)
                            installBrain.disabled = false;
                    }
                }
                if (installBrain.disabled)
                    installBrain.disabledReason = TranslatorFormattedStringExtensions.Translate("ShipInsideNoBrains");
                gizmos.Add(installBrain);
            }
            if(Props.canMergeAI && Consciousness==null)
            {
                Command_Action installCore = new Command_Action
                {
                    action = delegate
                    {
                        WhichPawn = parent.Map.listerThings.ThingsOfDef(ThingDefOf.AIPersonaCore).FirstOrDefault();
                    },
                    defaultLabel = TranslatorFormattedStringExtensions.Translate("ShipInsideInstallCore"),
                    defaultDesc = TranslatorFormattedStringExtensions.Translate("ShipInsideInstallCoreDesc"),
                    icon = ContentFinder<Texture2D>.Get("UI/ArchoTechUpload_Persona")
                };
                installCore.disabled = parent.Map.listerThings.ThingsOfDef(ThingDefOf.AIPersonaCore).FirstOrDefault() == null;
                if (installCore.disabled)
                    installCore.disabledReason = TranslatorFormattedStringExtensions.Translate("ShipInsideNoCores");
                gizmos.Add(installCore);
            }
            if(Props.mustBeDead && Consciousness!=null)
            {
                Command_Toggle resurrect = new Command_Toggle
                {
                    toggleAction = delegate
                    {
                        if (RezPlz == null)
                            RezPlz = parent.Map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("MechSerumResurrector")).FirstOrDefault();
                        else
                            RezPlz = null;
                    },
                    defaultLabel = TranslatorFormattedStringExtensions.Translate("ShipInsideResurrectBrain"),
                    defaultDesc = TranslatorFormattedStringExtensions.Translate("ShipInsideResurrectBrainDesc"),
                    icon = ContentFinder<Texture2D>.Get("Things/Item/Special/MechSerumResurrector"),
                    isActive = delegate
                    {
                        return RezPlz != null;
                    }
                };
                resurrect.disabled = parent.Map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("MechSerumResurrector")).FirstOrDefault() == null;
                if (resurrect.disabled)
                    resurrect.disabledReason = TranslatorFormattedStringExtensions.Translate("ShipInsideNoResurrector");
                gizmos.Add(resurrect);
            }
            return gizmos;
        }

        public void SpawnHologram()
        {
            GenPlace.TryPlaceThing(Consciousness, parent.Position, parent.Map, ThingPlaceMode.Near);
            Consciousness.Drawer.renderer.graphics.ResolveAllGraphics();
            if(Consciousness.Dead)
                ResurrectionUtility.Resurrect(Consciousness);
            if (!Consciousness.story.traits.HasTrait(TraitDefOf.Brawler) && Consciousness.skills.GetSkill(SkillDefOf.Melee).Level <= Consciousness.skills.GetSkill(SkillDefOf.Shooting).Level)
            {
                SwitchToRanged();
            }
            else
            {
                SwitchToMelee();
            }
            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(Find.CurrentMap);
            FleckMaker.Static(parent.Position, parent.Map, FleckDefOf.PsycastAreaEffect, 5f);
            Consciousness.ageTracker.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.ViaTreatment);
        }

        public override string CompInspectStringExtra()
        {
            if (Consciousness != null)
                return "Consciousness: " + Consciousness.Name.ToStringShort;
            else if (!Props.canMergeAI && !Props.canMergeHuman)
                return "AI name: " + AIName.Trim();
            else
                return base.CompInspectStringExtra();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            parent.Map.GetComponent<ShipHeatMapComp>().Spores.Add(this);
            compPower = parent.TryGetComp<CompPowerTrader>();
        }

        public override void PostDeSpawn(Map map)
        {
            map.GetComponent<ShipHeatMapComp>().Spores.Remove(this);
            if (ShipInteriorMod2.AirlockBugFlag)
                HologramDestroyed(false);
            base.PostDeSpawn(map);
        }

        public void InstallConsciousness(Thing newConsc, List<Apparel> overrideApparel=null, bool graphicsDirty=true)
        {
            if (Consciousness != null)
                return;
            Pawn pawn;
            if (newConsc is Corpse)
            {
                pawn = ((Corpse)newConsc).InnerPawn;
                List<Apparel> pawnApparel = pawn.apparel.WornApparel.ListFullCopy();
                pawn.Strip();
                ResurrectionUtility.Resurrect(pawn);
                Consciousness = pawn;
                SetupConsciousness(overrideApparel == null ? pawnApparel : overrideApparel, graphicsDirty);
            }
            else if (newConsc is Pawn)
            {
                pawn = (Pawn)newConsc;
                List<Apparel> pawnApparel = pawn.apparel.WornApparel.ListFullCopy();
                pawn.Strip();
                Consciousness = pawn;
                SetupConsciousness(overrideApparel==null ? pawnApparel : overrideApparel, graphicsDirty);
            }
            else
            {
                GenerateAIPawn();
                newConsc.DeSpawn();
            }
            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(Find.CurrentMap);
            FleckMaker.Static(parent.Position, parent.Map, FleckDefOf.PsycastAreaEffect, 5f);
        }

        public void ResurrectHologram(Thing serum)
        {
            if (Consciousness == null)
                return;
            Consciousness.Position = parent.Position;
            Consciousness.apparel.DropAll(parent.Position);
            Consciousness.equipment.DropAllEquipment(parent.Position);

            List<Hediff> hediffs = new List<Hediff>();
            foreach (Hediff hediff in Consciousness.health.hediffSet.hediffs)
            {
                hediffs.Add(hediff);
            }
            foreach (Hediff hediff in hediffs)
            {
                if (hediff.def.isBad || hediff.def == Props.holoHediff)
                    Consciousness.health.RemoveHediff(hediff);
            }
            Consciousness.needs.AddOrRemoveNeedsAsAppropriate();
            HologramColor = new Color(HologramColor.r, HologramColor.g, HologramColor.b, 1);
            Consciousness.story.HairColor = HologramColor;
            Consciousness.story.skinColorOverride = null;
            Consciousness.Drawer.renderer.graphics.SetAllGraphicsDirty();
            Consciousness.kindDef = PawnKindDefOf.Colonist;
            Consciousness.def = ThingDefOf.Human;
            typeof(Pawn_AgeTracker).GetMethod("RecalculateLifeStageIndex", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Consciousness.ageTracker, new object[] { });

            Consciousness.DeSpawn();
            Consciousness.SpawnSetup(parent.Map, false);
            Consciousness = null;
            serum.Destroy();
            RezPlz = null;
            WhichPawn = null;

            parent.DirtyMapMesh(parent.Map);
            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(Find.CurrentMap);
            FleckMaker.Static(parent.Position, parent.Map, FleckDefOf.PsycastAreaEffect, 5f);
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (signal == "PowerTurnedOff" || signal == "FlickedOff")
            {
                HologramDestroyed(false);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Consciousness != null && !Consciousness.Spawned)
                Consciousness.Tick();
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                if (Consciousness !=null && Consciousness.health.hediffSet.GetFirstHediff<HediffPawnIsHologram>() != null)
                {
                    HediffPawnIsHologram hologramHediff = (HediffPawnIsHologram)Consciousness.health.AddHediff(Props.holoHediff);
                    hologramHediff.consciousnessSource = (Building)parent;
                }
            }
        }

        void AddHolographicApparel(ThingDef def)
        {
            ApparelHolographic holoApp;
            if (def.apparel.layers.Contains(ApparelLayerDefOf.Overhead))
                holoApp = (ApparelHolographic)ThingMaker.MakeThing(ThingDef.Named("Apparel_HologramDummyHat"));
            else if (def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                holoApp = (ApparelHolographic)ThingMaker.MakeThing(ThingDef.Named("Apparel_HologramDummyShell"));
            else if (def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs))
                holoApp = (ApparelHolographic)ThingMaker.MakeThing(ThingDef.Named("Apparel_HologramDummyPants"));
            else if (def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                holoApp = (ApparelHolographic)ThingMaker.MakeThing(ThingDef.Named("Apparel_HologramDummyShirt"));
            else
                holoApp = (ApparelHolographic)ThingMaker.MakeThing(ThingDef.Named("Apparel_HologramDummyShell"));
            holoApp.apparelToMimic = def;
            Consciousness.apparel.Wear(holoApp);
            Consciousness.outfits.forcedHandler.SetForced(holoApp, true);
        }

        public void SwitchToRanged()
        {
            foreach (ThingWithComps equipped in Consciousness.equipment.AllEquipmentListForReading.ListFullCopy())
            {
                if(equipped.def.weaponClasses!=null && equipped.def.weaponClasses.Contains(DefDatabase<WeaponClassDef>.GetNamed("HologramGear")))
                    Consciousness.equipment.Remove(equipped);
            }
            Apparel belt = null;
            foreach(Apparel app in Consciousness.apparel.WornApparel)
            {
                if (app.TryGetComp<CompShield>() != null)
                {
                    belt = app;
                    break;
                }
            }
            if (belt != null)
                Consciousness.apparel.Remove(belt);
            Consciousness.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(Props.holoWeapon));
        }

        public void SwitchToMelee()
        {
            foreach (ThingWithComps equipped in Consciousness.equipment.AllEquipmentListForReading.ListFullCopy())
            {
                if (equipped.def.weaponClasses != null && equipped.def.weaponClasses.Contains(DefDatabase<WeaponClassDef>.GetNamed("HologramGear")))
                    Consciousness.equipment.Remove(equipped);
            }
            Consciousness.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(Props.holoWeaponMelee));
            Apparel app = (Apparel)ThingMaker.MakeThing(Props.holoShield);
            Consciousness.apparel.Wear(app);
            Consciousness.apparel.Lock(app);
        }
    }
}
