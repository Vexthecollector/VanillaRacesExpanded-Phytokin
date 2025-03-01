﻿
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
namespace VanillaRacesExpandedPhytokin
{
    public class CompVariablePollutionPump : ThingComp
    {
        private const int CheckInterval = 60;

        private CompPowerTrader compPower;

        private int ticksUntilPump;

        private int currentIntervalPumps;

        [LoadAlias("disabledByArtificalBuildings")]
        private bool disabledByArtificialBuildings;

        public CompProperties_VariablePollutionPump Props => (CompProperties_VariablePollutionPump)props;

        private bool Active
        {
            get
            {
                if (!parent.Spawned)
                {
                    return false;
                }
                if (disabledByArtificialBuildings)
                {
                    return false;
                }
                if (compPower != null && !compPower.PowerOn)
                {
                    return false;
                }
                if (!GetCellToUnpollute().IsValid)
                {
                    return false;
                }
                return true;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!ModLister.CheckBiotech("Pollution pump"))
            {
                parent.Destroy();
                return;
            }
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                ticksUntilPump = Props.intervalTicks;
            }
            compPower = parent.GetComp<CompPowerTrader>();
        }

        private IntVec3 GetCellToUnpollute()
        {
            int num = GenRadial.NumCellsInRadius(Props.radius+StaticCollectionsClass.polux_gene_colonists_inMap * 2);
            Map map = parent.Map;
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = parent.Position + GenRadial.RadialPattern[i];
                if (intVec.InBounds(map) && intVec.CanUnpollute(map))
                {
                    return intVec;
                }
            }
            return IntVec3.Invalid;
        }

        private void Pump()
        {
            IntVec3 cellToUnpollute = GetCellToUnpollute();
            if (!cellToUnpollute.IsValid)
            {
                return;
            }
            Map map = parent.Map;
            map.pollutionGrid.SetPolluted(cellToUnpollute, isPolluted: false);
            currentIntervalPumps++;
            if (Props.pumpEffecterDef != null)
            {
                Props.pumpEffecterDef.Spawn(parent.Position, map).Cleanup();
            }
            if (Props.pumpsPerWastepack > 0 && currentIntervalPumps % Props.pumpsPerWastepack == 0)
            {
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDefOf.Wastepack), parent.Position, parent.Map, ThingPlaceMode.Near, null, (IntVec3 p) => p.GetFirstBuilding(map) == null);
                currentIntervalPumps = 0;
            }
            MoteMaker.MakeAttachedOverlay(parent, ThingDefOf.Mote_PollutionPump, Vector3.zero);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }
            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "DEV: Pump";
            command_Action.action = Pump;
            command_Action.disabled = !Active;
            yield return command_Action;
            yield return new Command_Action
            {
                defaultLabel = "DEV: Set next pump time",
                action = delegate
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();
                    int[] array = new int[11]
                    {
                    60, 120, 180, 240, 300, 600, 900, 1200, 1500, 1800,
                    3600
                    };
                    foreach (int ticks in array)
                    {
                        list.Add(new FloatMenuOption(ticks.ToStringSecondsFromTicks("F0"), delegate
                        {
                            ticksUntilPump = ticks;
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(list));
                },
                disabled = !Active
            };
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.CompInspectStringExtra());
            if (stringBuilder.Length > 0)
            {
                stringBuilder.AppendLine();
            }
            if (Active)
            {
                stringBuilder.Append("AbsorbingPollutionNext".Translate(ticksUntilPump.ToStringTicksToPeriod()));
            }
            else if (disabledByArtificialBuildings)
            {
                stringBuilder.Append("CannotAbsorbPollution".Translate());
            }
            else
            {
                stringBuilder.Append("CanAbsorbPollution".Translate());
            }
            return stringBuilder.ToString();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Props.disabledByArtificialBuildings && Find.TickManager.TicksGame % CheckInterval == 0)
            {
                disabledByArtificialBuildings = Find.CurrentMap.listerArtificialBuildingsForMeditation.GetForCell(parent.Position, Props.radius + StaticCollectionsClass.polux_gene_colonists_inMap * 2).Count > 0;
            }
            if (parent.IsHashIntervalTick(CheckInterval) && Active)
            {
                ticksUntilPump -= CheckInterval;
                if (ticksUntilPump <= 0)
                {
                    ticksUntilPump = Props.intervalTicks;
                    Pump();
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilPump, "ticksUntilPump", 0);
            Scribe_Values.Look(ref currentIntervalPumps, "currentIntervalPumps", 0);
            Scribe_Values.Look(ref disabledByArtificialBuildings, "disabledByArtificialBuildings", defaultValue: false);
        }
    }
}
