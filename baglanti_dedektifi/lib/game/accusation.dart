import '../data/models.dart';

enum AccusationOutcome {
  /// Dogru kisi ve iki gecerli kanit.
  correct,

  /// Dogru kisi ama kanitlarin en az biri onu kanitlamiyor.
  weakEvidence,

  /// Yanlis kisi.
  wrongSuspect,
}

/// Suclamada secilmesi gereken kanit sayisi.
const requiredProofCount = 2;

AccusationOutcome judgeAccusation(
  CaseFile caseFile,
  String suspectId,
  Set<String> clueIds,
) {
  if (suspectId != caseFile.culpritId) return AccusationOutcome.wrongSuspect;
  final valid = clueIds.length == requiredProofCount &&
      clueIds.every(caseFile.proofClueIds.contains);
  return valid ? AccusationOutcome.correct : AccusationOutcome.weakEvidence;
}

/// Dava yildizi, dogru suclamaya kadar yapilan deneme sayisina gore.
int caseStarsForAttempts(int attempts) {
  if (attempts <= 1) return 3;
  if (attempts == 2) return 2;
  return 1;
}
