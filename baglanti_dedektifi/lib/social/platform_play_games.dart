// Web'de games_services eklentisini ice aktarma.
export 'platform_play_games_stub.dart'
    if (dart.library.io) 'platform_play_games_mobile.dart';
