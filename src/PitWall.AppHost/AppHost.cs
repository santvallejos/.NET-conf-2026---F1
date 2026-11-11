var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.PitWall_Web>("web");

builder.Build().Run();
