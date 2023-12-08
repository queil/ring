﻿namespace Queil.Ring.Configuration.Interfaces;

public interface IUseCsProjFile : IUseWorkingDir
{
    string CsProj { get; set; }
    string FullPath { get; }
    string LaunchSettingsJsonPath { get; }
    string Configuration { get; }
}
