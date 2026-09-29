namespace CassieReplacement
{
    using System.Globalization;
    using CassieReplacement.Config;
    using CassieReplacement.Playback;
    using CassieReplacement.Reader;
    using CassieReplacement.Reader.Enums;
    using PlayerRoles;
    using PlayerStatsSystem;

    /// <summary>
    /// Buduje i odtwarza komunikaty o falach i śmierci SCP.
    /// REFACTOR: klasa statyczna (nie zawiera już żadnego stanu ani martwych handlerów EXILED).
    /// </summary>
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

            // FIX: nieobsługiwana frakcja = brak komunikatu (wcześniej pusty CassieAnnouncement z null w polach -> NRE).
            CassieAnnouncement template = faction switch
            {
                Faction.FoundationStaff => isMiniWave ? config.NtfMiniAnnouncement : config.NtfWaveAnnouncement,
                Faction.FoundationEnemy => isMiniWave ? config.ChaosMiniAnnouncement : config.ChaosWaveAnnouncement,
                _ => null,
            };

            if (template == null)
            {
                return;
            }

            template.GenericReplacement()
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

            // FIX: TryGetValue z fallbackiem zamiast indeksatora - brak klucza w configu (np. SCP-173) rzucał KeyNotFoundException
            // wewnątrz prefiksu Harmony, czyli w środku obsługi śmierci gracza.
            CassieAnnouncement deathCause = config.DamageTypeTerminationAnnouncementLookupTable.TryGetValue(damageType, out CassieAnnouncement cause)
                ? cause
                : config.DamageTypeTerminationAnnouncementLookupTable.TryGetValue(CassieDamageType.Unknown, out CassieAnnouncement unknown)
                    ? unknown
                    : new CassieAnnouncement();

            CassieAnnouncement team = config.TeamTerminationCallsignLookupTable.TryGetValue(attackerRole.GetTeam(), out CassieAnnouncement callSign)
                ? callSign
                : new CassieAnnouncement();

            // Kolejność Replace ma znaczenie: {deathcause} wprowadza {team}, {team} wprowadza {scpkiller}/{letter}/{number}.
            config.ScpTerminationAnnouncement.GenericReplacement()
                .Replace("{scp}", ResolveScp(config, victimRole))
                .Replace("{deathcause}", deathCause)
                .Replace("{team}", team)
                .Replace("{scpkiller}", attackerRole.GetTeam() == Team.SCPs ? ResolveScp(config, attackerRole) : new CassieAnnouncement())
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

        /// <summary>Wpis z configu, a dla roli spoza tabeli (np. nowy SCP) - komunikat wygenerowany z nazwy roli ("Scp173" -> "scp 1 7 3").</summary>
        private static CassieAnnouncement ResolveScp(CassieOverrideConfigs config, RoleTypeId role)
        {
            if (config.ScpLookupTable.TryGetValue(role, out CassieAnnouncement entry))
            {
                return entry;
            }

            string name = role.ToString();
            const string scpPrefix = "Scp";
            if (!name.StartsWith(scpPrefix) || name.Length == scpPrefix.Length)
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

    /// <summary>DRY: parsowanie nazwy jednostki ("EPSILON-11") było zduplikowane w handlerze śmierci i w patchu fal.</summary>
    public static class CassieUnit
    {
        public static bool TryParse(string unitName, out string letter, out int number)
        {
            letter = string.Empty;
            number = 0;

            if (string.IsNullOrWhiteSpace(unitName))
            {
                return false;
            }

            int dash = unitName.IndexOf('-');
            if (dash <= 0 || dash == unitName.Length - 1)
            {
                return false;
            }

            letter = unitName.Substring(0, dash);

            // FIX: TryParse zamiast int.Parse (FormatException dla niestandardowych nazw jednostek).
            return int.TryParse(unitName.Substring(dash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }
    }
}
