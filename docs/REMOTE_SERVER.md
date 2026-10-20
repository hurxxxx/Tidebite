# 원격 서버 접속 및 운영 인수인계

마지막 확인: **2026-10-20 17:13, 한국 시간**. 이 문서는 실제 설치된 서버의 접속 정보와 작업 맥락입니다. 일반적인 신규 설치 절차는 [REMOTE_TESTING.md](REMOTE_TESTING.md), 계정/API 설계는 [ACCOUNT_SERVICES.md](ACCOUNT_SERVICES.md)를 참고합니다. 비밀번호·개인 키·방 인증 키·세션 토큰은 이 문서에 저장하지 않습니다.

## 현재 결정과 상태

- **게임 통신은 기존 UDP를 유지합니다.** 10월 19일에는 공인 UDP 전달이 실패해 로컬 서버로 테스트했으며, **10월 20일 재검사에서 공인 UDP 접속이 성공했습니다.** TCP/WebSocket 전환은 적용하지 않았습니다. 로컬 테스트 환경도 계속 사용할 수 있습니다.
- 원격에는 Rust 계정/매칭 서버와 Unity Linux 전용 게임 서버를 설치해 실행 중입니다. 로컬 테스트로 전환했다고 원격 서비스를 삭제하거나 중지한 상태는 아닙니다.
- 내부 UDP와 Tailscale에 이어, 공인 `14.39.187.106:37900`에도 VPN 경로 대체 없이 서명 입장과 15초/422개 스냅샷 수신을 확인했습니다. 계정 API는 개발용 SSH 터널을 사용하므로 공개 HTTPS API 구성은 별도로 남아 있습니다.
- 소스는 **GitHub clone + pull**로 배포합니다. Unity가 생성한 Linux 빌드만 별도 전송합니다.

## SSH 접속

현재 개발 Mac에서 프로젝트 루트는 `/Users/edward/projects/games`입니다. 원격 접속:

```bash
ssh -p 31869 user@14.39.187.106
cd ~/projects/Tidebite
```

Giz 관리 화면의 SSH 이름은 `dmc1.giz.sh`이고, 화면에 표시된 대체 명령은 `ssh -p 31869 user@dmc1.giz.sh`입니다. 실제 작업에서 검증한 명령은 위 공인 IP 명령입니다. 비대화형 점검에는 다음처럼 접속합니다.

```bash
ssh -p 31869 -o BatchMode=yes -o ConnectTimeout=10 user@14.39.187.106
```

이 Mac에서는 기존 `~/.ssh/id_ed25519` 키 기반 인증으로 접속했습니다. 다른 개발 기기는 해당 기기의 공개 키 등록이 필요합니다. 개인 키 내용은 복사하거나 Git에 넣지 않습니다.

| 항목 | 확인된 값 |
| --- | --- |
| 원격 OS / 아키텍처 | Ubuntu 24.04 계열 / x86_64 |
| 호스트 이름 | `lumejs` |
| SSH 사용자 / 공인 포트 | `user` / TCP `31869` |
| 원격 소스 경로 | `/home/user/projects/Tidebite` |
| Git 원격 / 브랜치 | `https://github.com/hurxxxx/Tidebite.git` / `main` |
| 서버 내부 IPv4 | `10.173.130.111` — 환경에 따라 변경 가능 |
| 서버 Tailscale IPv4 | `100.76.191.22` |
| 당시 개발 Mac의 Tailscale IPv4 | `100.112.153.112` |
| Docker / Compose | `29.1.3` / `2.37.1` |

## 포트와 실제 통신 경로

| 용도 | 외부에서 사용하는 주소 | 서버 내부 대상 | 현재 상태 |
| --- | --- | --- | --- |
| SSH | `14.39.187.106:31869` TCP | SSH 서비스 | 접속 확인 |
| 실제 게임 | `14.39.187.106:37900` **UDP** | 호스트 UDP `7777` → ocean 컨테이너 UDP `7777` | 2026-10-20 공인 게임 접속 확인 |
| 게임의 VPN 경로 | `100.76.191.22:7777` UDP | 같은 ocean | 실제 게임 접속 확인 |
| Rust 계정/매칭 API | 개발 Mac의 SSH 터널 `127.0.0.1:18080` | 호스트 `127.0.0.1:8080` → matchmaker 컨테이너 TCP `8080` | 정상 |
| TCP 전달 진단 | `14.39.187.106:37368` TCP | 임시 TCP `7777` 응답기 | 진단 성공 후 응답기 종료 |

Giz의 UDP 매핑은 **외부 37900 → 내부 7777**입니다. 외부 게임 주소에 `:7777`을 넣지 않습니다. 내부 게임 서버의 수신 포트는 계속 7777입니다. 중앙 서비스가 클라이언트에 배정하는 주소는 `14.39.187.106:37900`으로 설정되어 있습니다.

TCP 37368의 성공은 TCP 포트 전달만 검증한 것입니다. 현재 게임은 UDP이며 상시 TCP 7777 리스너는 없습니다. 진단용 TCP 응답기·패킷 캡처·임시 SSH 터널은 모두 종료했습니다. 공개 HTTPS 계정 API는 아직 구성하지 않았으며, 외부 사용자가 VPN 없이 서비스 전체를 이용할 준비가 완료된 상태는 아닙니다.

## 서비스와 영구 데이터

원격 `~/projects/Tidebite`에서 모든 Compose 명령을 실행합니다.

| Compose 서비스 | 현재 컨테이너 이름 | 역할 / 저장소 |
| --- | --- | --- |
| `matchmaker` | `tidebite-matchmaker-1` | Rust/Axum 인증·계정·프로필·매칭·경기 기록·순위 |
| `ocean` | `tidebite-ocean-1` | Unity Linux 전용 서버, 전투·봇·결과의 권한 보유 |
| 볼륨 `accounts` | `tidebite_accounts` | `/data/accounts.sqlite3` 및 SQLite WAL/SHM |
| 볼륨 `ocean_reports` | `tidebite_ocean_reports` | `/data/results-outbox.json` 및 백업 |

두 서비스는 `restart: unless-stopped`, 비루트 UID 10001로 실행합니다. 계정 서버는 256 MiB/CPU 0.5개, 게임 서버는 512 MiB/CPU 1개 제한입니다. 정상 게임 명령은 `--port 7777`이며 라운드는 기본 **240초**입니다. `.local/smoke.compose.yaml`의 30초 라운드는 테스트할 때만 명시적으로 적용합니다.

인증 반복 시 Linux 메모리 증가를 줄이도록 `MALLOC_ARENA_MAX=2`, `MALLOC_MMAP_THRESHOLD_=131072`, Tokio 스레드 상한과 동시 비밀번호 해시 2개 제한을 적용했습니다. native 검사에서 계정 컨테이너 최고 메모리는 약 24.2 MiB, 재시작/OOM 이벤트는 0이었습니다. 장기 부하 결과는 아닙니다.

기존 `codex-console-upstream`, `minio`는 Tidebite와 별개입니다. 서비스 재배포 때 변경하지 않습니다. 영구 볼륨을 지우는 `docker compose down -v`나 전역 Docker 정리는 이 운영 절차에 포함하지 않습니다. 실행 중 SQLite 백업은 SQLite backup API를 사용해야 하며 WAL을 무시하고 본체 파일만 복사하지 않습니다.

## 상태와 로그 확인

```bash
cd ~/projects/Tidebite
git status --short
git log -1 --oneline
docker compose ps
curl -fsS http://127.0.0.1:8080/health
docker compose logs --tail=100 matchmaker ocean
ss -lun 'sport = :7777'
```

확인 당시 health 응답은 `{"protocol":11,"status":"ok"}`였고 두 컨테이너의 재시작 횟수는 0이었습니다. health 성공은 API 생존 검사이며 공인 UDP 게임 접속 성공을 뜻하지 않습니다. `.local/online.env`나 전체 컨테이너 환경 변수에는 인증 키가 있으므로 출력/공유하지 않습니다.

## 설정 파일 위치

| 위치 | 내용 |
| --- | --- |
| 원격 `.local/rooms.json` | 방 ID `ocean-1`, 공인 UDP endpoint, 비밀 방 키 |
| 원격 `.local/online.env` | 같은 방 ID·endpoint·키를 서비스에 전달 |
| 원격 `.local/smoke.compose.yaml` | native 통합 검사에 사용하는 30초 라운드 override |
| Mac `.local/remote-target.json` | SSH 대상·포트, 로컬 API 터널 포트, 개발 VPN 경로 |
| `Assets/Tidebite/Resources/OnlineSettings.json` | 배포용 클라이언트 API URL 설정; 공개 HTTPS 주소는 아직 미설정 |

`.local/`은 Git에서 제외됩니다. 기존 원격 방 ID/키를 재생성하지 않습니다. endpoint를 바꿀 때는 원격의 위 두 설정 파일에서 주소만 일치시킨 뒤 `docker compose up -d`로 반영합니다. `configure-online.py`는 신규 설치용이며 기존 `rooms.json`이 있으면 보존하고 종료합니다.

Mac의 현재 `.local/remote-target.json` 내용은 다음과 같습니다. 이 파일 자체는 Git에서 제외합니다.

```json
{
  "sshTarget": "user@14.39.187.106",
  "sshPort": 31869,
  "apiLocalPort": 18080,
  "gameEndpointOverride": "100.76.191.22:7777"
}
```

## Mac에서 원격 서버 테스트

프로젝트 루트에서 실행합니다. 첫 명령은 API만 검사하며 게임 창을 열지 않습니다. 두 번째 명령은 SSH 터널과 **게임 창 하나**를 열고 기존 개발 VPN 경로로 접속합니다.

```bash
python3 scripts/remote-client.py --check
python3 scripts/remote-client.py
```

공인 UDP 경로는 2026-10-20 검사를 통과했습니다. Mac의 기존 설정에는 VPN override가 남아 있으므로 **공인 경로를 시험할 때는 반드시 `--public`을 사용합니다.**

```bash
python3 scripts/remote-client.py --public
```

`--public`은 API의 SSH 터널을 유지하면서 게임 UDP에 중앙 배정 주소를 사용합니다. 로그인/가입 후 함께 플레이로 확인합니다. VPN 경로는 Development 전용 옵션으로만 대체하며 서명 입장권·서버 인증은 유지합니다. Release 빌드는 해당 진단 옵션을 무시합니다.

공인 경로의 자동 검사는 터미널 하나에서 다음 API 터널을 유지하고:

```bash
ssh -N -L 127.0.0.1:18080:127.0.0.1:8080 -p 31869 user@14.39.187.106
```

다른 터미널의 프로젝트 루트에서 실행합니다. 검사 후 첫 터미널의 Ctrl-C로 수동 터널을 종료합니다. 이미 자동 launcher가 같은 터널 포트를 쓰고 있다면 먼저 닫습니다.

```bash
python3 scripts/test-remote-client.py --label public-after-fix
# 필요할 때만 VPN 대조 검사:
python3 scripts/test-remote-client.py --endpoint 100.76.191.22:7777 --label vpn-control
```

검사는 실제 임시 계정을 만들고 서명 입장권으로 headless 클라이언트를 실행한 뒤 해당 계정을 삭제합니다. 로컬 보고서는 Git에서 제외된 `TestResults/remote-client-<label>/report.json`에 남습니다. Native Linux의 두 사용자·경기 기록 검사는 [REMOTE_TESTING.md](REMOTE_TESTING.md#native-linux-integration-test)를 따르며 종료 후 기본 4분 라운드를 복원합니다.

## 업데이트 / 재시작

소스 업데이트는 원격에서:

```bash
cd ~/projects/Tidebite
git pull --ff-only
```

Rust 코드나 계정 서버 Dockerfile 변경 시:

```bash
docker compose build matchmaker
docker compose up -d matchmaker
```

게임 코드 변경 시에는 **Mac에서** 같은 소스 체크포인트의 Unity Linux 빌드를 생성하고, 빌드 생성물만 업로드합니다.

```bash
bash scripts/build.sh linux
COPYFILE_DISABLE=1 tar --no-xattrs -czf - \
  --exclude='Tidebite_BurstDebugInformation_DoNotShip' Builds/LinuxServer | \
  ssh -p 31869 user@14.39.187.106 'cd "$HOME/projects/Tidebite" && tar -xzf -'
```

업로드 후 **원격에서** 게임 이미지를 다시 빌드합니다. 소스 pull과 빌드 생성물이 같은 코드인지 확인합니다.

```bash
cd ~/projects/Tidebite
docker compose build ocean
docker compose up -d ocean
docker compose ps
```

문서만 변경하면 pull만으로 충분합니다. 컨테이너 재시작이 필요한 경우에만 `docker compose restart matchmaker ocean`을 사용합니다. DB와 결과 보관함은 볼륨에 유지됩니다. 운영 중 게임 컨테이너 재생성은 진행 중 접속을 끊으므로 플레이 테스트와 겹치지 않게 합니다.

## 검증 체크포인트와 남은 작업

**2026-10-20 17:13 KST 최신 결과:** 중앙에서 배정한 `14.39.187.106:37900`에 `--endpoint`/VPN 대체 옵션 없이 실제 headless 게임 클라이언트가 입장했습니다. 약 15초 동안 422개 스냅샷을 수신했고 `passed=true`, `ready=true`, `usedEndpointOverride=false`입니다. 서버 UDP 7777 캡처에서도 요청·응답 양방향을 확인했습니다. 검사 계정은 삭제했으며 임시 API 터널과 캡처는 종료했습니다. 기존 게임 서비스/설정은 변경하지 않았습니다. 결과는 Mac의 `TestResults/remote-client-public-20261020T081256Z/report.json`, 서버 캡처는 원격 `TestResults/udp-public-check-20261020.log`에 있습니다. 아래 10월 19일의 실패 기록은 이 결과로 갱신됩니다.

2026-10-19 확인 시 애플리케이션 소스 체크포인트는 `637e9f4`였습니다. 이후 문서 전용 커밋은 런타임 변경이 아닙니다. 배포된 `Builds/LinuxServer/TidebiteServer_Data/Managed/Tidebite.Runtime.dll`의 SHA-256:

```text
4ac946bb2f205bbab3f3956e426f3e6ebc2272ddeff75ed68e7ac6b125bace8c
```

- 원격 native Linux 두 사용자: 서로 다른 슬롯, 1,235 / 1,327개 스냅샷, 가입·로그인·프로필·경기 종료·기록·리더보드 통과. 컨테이너 재생성 후 기존 세션/프로필 유지도 확인했습니다.
- Mac → 원격 VPN: 이전 검사 15초/573개, **최신 재검사 15초/367개 스냅샷**으로 실제 게임 접속 성공.
- **2026-10-19 21:07 KST 재검사:** TCP `37368 → 7777` 임시 응답 정상. 공인 UDP `37900 → 7777`은 스냅샷 0개. 이후 UDP 진단 패킷 캡처에서 공인 경로 패킷은 보이지 않았고 VPN 패킷은 도착했습니다. 원인이 Giz 게이트웨이인지 중간망/클라이언트 송신망인지는 확정하지 않았습니다.
- 생성한 테스트 계정·임시 리스너·캡처는 정리했습니다. 게임 서비스 두 개는 유지합니다.
- 로컬 근거: `TestResults/deployed-linux-remote.json`, `TestResults/remote-persistence.json`, `TestResults/public-route-recheck/report.json`, `TestResults/public-route-recheck/udp-packets.log`. 원격 native 보고서는 `~/projects/Tidebite/TestResults/deployed-linux/report.json`입니다. 이 결과 파일들은 Git에 포함되지 않으므로 위 요약이 인수인계 기록입니다.
- 남은 작업: 도메인/HTTPS 계정 API, 모바일 실기기 지연·손실·복귀 테스트, 장기/동시 접속 부하, 정기 백업. 최신 공인 UDP 검사는 단일 클라이언트의 짧은 접속 검사이며 출시 완료나 64명 부하 통과를 의미하지 않습니다.

## 그동안 로컬에서 개발하기

Mac 프로젝트 루트에서 아래 명령으로 Rust 계정 서비스·headless 게임 서버·그래픽 클라이언트 하나를 실행합니다. 원격 컨테이너와 원격 계정 DB는 사용하지 않습니다.

```bash
python3 scripts/local-online.py --client
```

로컬 기본 주소는 API `http://127.0.0.1:8080`, 게임 UDP `127.0.0.1:7777`입니다. 로컬 계정 DB는 `.local/accounts.sqlite3`, 결과 보관함은 `.local/outbox.json`입니다. 로컬과 원격의 계정은 별개입니다. 로컬 `.local/rooms.json`의 endpoint도 loopback인지 먼저 확인합니다. 실행 터미널의 Ctrl-C는 이 launcher가 만든 프로세스만 종료합니다. 자세한 로그는 `Logs/local-online-*.log`에 남습니다.
