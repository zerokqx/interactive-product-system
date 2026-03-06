{
  pkgs,
  ...
}:
let
  dotnet8 = "${pkgs.dotnetCorePackages.sdk_8_0-bin}/share/dotnet/dotnet";
in
{
  env.GREET = "devenv";
  packages = [
    pkgs.git
  ];

  scripts.hello.exec = ''
    echo hello from $GREET
  '';

  scripts.run.exec = ''${dotnet8} run'';
  scripts.run-dev.exec = ''${dotnet8} watch run'';
  enterShell = ''
    export PATH="$DOTNET_ROOT/bin:$PATH"
     hello         # Run scripts directly
     git --version # Use packages
  '';
  languages.dotnet.enable = true;
}
