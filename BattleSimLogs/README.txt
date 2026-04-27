Battle Simulation report folder (project root, outside Assets/)

After running Battle Simulator from Unity, the report is written here by default:
  _LastBattleSimReport.txt

If the file does not appear:
  - Run the simulation once from the BattleSimulator component (Inspector).
  - Ensure "Write Report File" is enabled and Report Relative Path is:
      BattleSimLogs/_LastBattleSimReport.txt
    (Paths under Assets/ can cause Unity import errors when overwritten.)
