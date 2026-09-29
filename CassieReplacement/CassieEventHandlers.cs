namespace CassieReplacement
{
    using System;
    using System.Globalization;
    using CassieReplacement.Config;
    using CassieReplacement.Reader;
    using CassieReplacement.Reader.Enums;
    using PlayerRoles;
    using PlayerStatsSystem;

    public static class CassieEventHandlers
    {
        private const string DefaultUnitLetter = "a";

        public static void HandleAnnouncingWaveEntrance(Faction faction, bool isMiniWave, string unitLetter = "", int unitNumber = 0)
        {
            CassieOverrideConfigs config = Plugin.Singleton?.Config?.CassieOverrideConfig;
            if (config == null)
            {
                return;
            }

            CassieAnnouncement template = faction switch
            {
                Faction.FoundationStaff => isMiniWave ? config.NtfMiniAnnouncement : config.NtfWaveAnnouncement,
                Faction.FoundationEnemy => isMiniWave ? config.ChaosMiniAnnouncement : config.ChaosWaveAnnouncement,
                _ => null,
            };

            template?.GenericReplacement()
                .Replace("{letter}", CreateUnitLetter(unitLetter))
                .Replace("{number}", CreateUnitNumber(unitNumber))
                .Announce();
        }

        public static void HandleAnnouncingTermination(DamageHandlerBase damageHandler, RoleTypeId victimRole)
        {
            CassieOverrideConfigs config = Plugin.Singleton?.Config?.CassieOverrideConfig;
            if (config == null)
            {
                return;
            }

            CassieDamageType damageType = ResolveDamageType(damageHandler, out RoleTypeId attackerRole, out string attackerUnit);

            CassieAnnouncement letter = new CassieAnnouncement();
            CassieAnnouncement number = new CassieAnnouncement();
            if (CassieUnit.TryParse(attackerUnit, out string unitLetter, out int unitNumber))
            {
                letter = CreateUnitLetter(unitLetter);
                number = CreateUnitNumber(unitNumber);
            }

            Team attackerTeam = attackerRole.GetTeam();
            CassieAnnouncement team = config.TeamTerminationCallsignLookupTable.TryGetValue(attackerTeam, out CassieAnnouncement callSign)
                ? callSign
                : new CassieAnnouncement();
            CassieAnnouncement scpKiller = attackerTeam == Team.SCPs ? ResolveScp(config, attackerRole) : new CassieAnnouncement();

            config.ScpTerminationAnnouncement.GenericReplacement()
                .Replace("{scp}", ResolveScp(config, victimRole))
                .Replace("{deathcause}", FindDeathCause(config, damageType))
                .Replace("{team}", team)
                .Replace("{scpkiller}", scpKiller)
                .Replace("{letter}", letter)
                .Replace("{number}", number)
                .Announce();
        }

        private static CassieDamageType ResolveDamageType(DamageHandlerBase handler, out RoleTypeId attackerRole, out string attackerUnit)
        {
            attackerRole = RoleTypeId.None;
            attackerUnit = string.Empty;

            switch (handler)
            {
                case AttackerDamageHandler attacker:
                    attackerRole = attacker.Attacker.Role;
                    attackerUnit = attacker.Attacker.UnitName;
                    return CassieDamageType.Player;
                case WarheadDamageHandler:
                    return CassieDamageType.Warhead;
                case UniversalDamageHandler universal when universal.TranslationId == DeathTranslations.Decontamination.Id:
                    return CassieDamageType.Decontamination;
                case UniversalDamageHandler universal when universal.TranslationId == DeathTranslations.Tesla.Id:
                    return CassieDamageType.Tesla;
                default:
                    return CassieDamageType.Unknown;
            }
        }

        private static CassieAnnouncement FindDeathCause(CassieOverrideConfigs config, CassieDamageType damageType)
        {
            if (config.DamageTypeTerminationAnnouncementLookupTable.TryGetValue(damageType, out CassieAnnouncement cause)
                || config.DamageTypeTerminationAnnouncementLookupTable.TryGetValue(CassieDamageType.Unknown, out cause))
            {
                return cause;
            }

            return new CassieAnnouncement();
        }

        private static CassieAnnouncement ResolveScp(CassieOverrideConfigs config, RoleTypeId role)
        {
            if (config.ScpLookupTable.TryGetValue(role, out CassieAnnouncement entry))
            {
                return entry;
            }

            const string scpPrefix = "Scp";
            string name = role.ToString();
            if (!name.StartsWith(scpPrefix, StringComparison.Ordinal) || name.Length == scpPrefix.Length)
            {
                return new CassieAnnouncement();
            }

            string digits = name.Substring(scpPrefix.Length);
            return new CassieAnnouncement("scp " + string.Join(" ", digits.ToCharArray()), "SCP-" + digits);
        }

        private static CassieAnnouncement CreateUnitLetter(string unitLetter)
        {
            string trimmed = unitLetter?.Trim();
            string first = string.IsNullOrEmpty(trimmed) ? DefaultUnitLetter : trimmed.Substring(0, 1);
            return new CassieAnnouncement($"nato_{first}", trimmed);
        }

        private static CassieAnnouncement CreateUnitNumber(int unitNumber)
        {
            return new CassieAnnouncement(
                unitNumber.ToString(CultureInfo.InvariantCulture),
                unitNumber.ToString("00", CultureInfo.InvariantCulture));
        }
    }
}
