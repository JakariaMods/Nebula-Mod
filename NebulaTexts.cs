using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jakaria
{
    public static class NebulaTexts
    {
        public const string NebulaModVersion = "Nebula Mod V{0} by Jakaria.";

        public const string NebulaToggleDebug = "Toggled debug drawing of nebulae";

        public const string NebulaCreate = "Created a new nebula.";
        public const string NebulaReset = "Reset the nebula's settings.";
        public const string NebulaRegen = "Regenerated the nebula's seed.";
        public const string NebulaRemove = "Removed the closest nebula.";
        public const string NebulaClear = "Cleared the current weather.";

        public const string NebulaSetDensity = "Set the nebula's density to {0}.";
        public const string NebulaSetSeed = "Set the nebula's seed to {0}.";
        public const string NebulaSetPrimaryColor = "Set the nebula's primary color.";
        public const string NebulaSetSecondaryColor = "Set the nebula's secondary color.";
        public const string NebulaSetScale = "Set the nebula's noise scale to {0}.";
        public const string NebulaSetColorScale = "Set the nebula's color noise scale to {0}.";
        public const string NebulaSetRadius = "Set the nebula's radius to {0}km.";
        public const string NebulaSetFrequency = "Set the nebula's weather frequency to between {0} and {1} seconds.";
        public const string NebulaSetLength = "Set the nebula's weather length to between {0} and {1} seconds.";

        public const string NebulaGetDensity = "The nebula's density is {0}.";
        public const string NebulaGetSeed = "The nebula's seed is {0}.";
        public const string NebulaGetPrimaryColor = "The nebula's primary color is {0}";
        public const string NebulaGetSecondaryColor = "The nebula's secondary color is {0}";
        public const string NebulaGetScale = "The nebula's noise scale is {0}.";
        public const string NebulaGetColorScale = "The nebula's color noise scale is {0}.";
        public const string NebulaGetRadius = "The nebula's radius is {0}km.";
        public const string NebulaGetFrequency = "The nebula's weather frequency is between {0} and {1} seconds.";
        public const string NebulaGetLength = "The nebula's weather length is between {0} and {1} seconds.";
        public const string NebulaGetWeather = "{0} is currently occuring";

        public const string NebulaSpawnWeather = "Spawned {0}";
        public const string NebulaListWeather = "Available weathers: {0}";
        public const string NebulaSpawnRandomWeather = "Spawned random weather";
        public const string NebulaSpawnWeatherFail = "Weather is already occuring";

        public const string NoParseFloat = "Couldn't parse float {0}.";
        public const string NoParseInt = "Couldn't parse int {0}.";
        public const string NoParseWeather = "Couldn't find weather {0}.";
        public const string NoNebula = "You're not inside a nebula.";
        public const string NoRoom = "You're too close to another nebula.";
        public const string NoPermissions = "You're not a high enough rank to do this.";
        public const string NoWeather = "No weather is occuring.";
        public const string ExpectedParameters2 = "Expected two parameters.";
        public const string ExpectedParameters3 = "Expected three parameters.";
        public const string ExpectedParameters4 = "Expected four parameters.";
    }
}
