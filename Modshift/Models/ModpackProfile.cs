using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Modshift.Models;

public class ModpackProfile
{
    public Version SchemaVersion { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string Version { get; set; }
    public string Loader { get; set; }
    public string ModsFolderPath { get; set; }
    public string Description { get; set; }
}