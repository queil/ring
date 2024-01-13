﻿namespace Queil.Ring.Configuration.Interfaces;

public interface IConfigurationLoader
{
    T Load<T>(string path) where T : class, new();
}
