﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2015 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DaggerfallWorkshop.Demo.Entity
{
    /// <summary>
    /// Flags for special abilities.
    /// </summary>
    [Flags]
    public enum SpecialAbilityFlags
    {
        None = 0,
        AcuteHearing = 1,
        Athleticism = 2,
        AdrenalineRush = 4,
        NoRegenSpellPoints = 8,
        SunDamage = 16,
        HolyDamage = 32,
    }
}