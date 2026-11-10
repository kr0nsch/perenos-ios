procedure MovePreviousInstallationDirectory(const PreviousDir, NewDir: String);
begin
  if not DirExists(PreviousDir) or DirExists(NewDir) then
    Exit;
  if not RenameFile(PreviousDir, NewDir) then
    RaiseException(CustomMessage('MigrationFailed'));
end;
