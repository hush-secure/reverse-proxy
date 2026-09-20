import { useEffect, useMemo, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import {
  Activity,
  Database,
  ShieldAlert,
  Server,
  CheckCircle2,
  XCircle,
  ArrowUp,
  ArrowDown,
  Minus,
  Radio,
} from 'lucide-react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  BarChart,
  Bar,
  Cell,
  PieChart,
  Pie,
} from 'recharts';
import { useTheme } from './hooks/useTheme';
import ThemeSwitcher from './components/ThemeSwitcher';
import styles from './css/App.module.scss';

export interface ServerStatusDto {
  id: string;
  url: string;
  isHealthy: boolean;
  weight: number;
}

export interface DashboardStatsDto {
  totalRequests: number;
  cacheHits: number;
  cacheMisses: number;
  cacheHitRatio: number;
  rateLimitBlocked: number;
  backendServers: ServerStatusDto[];
  timestamp: string;
}

interface HistoryPoint {
  time: string;
  totalRequests: number;
  cacheHitRatio: number;
  rateLimitBlocked: number;
}

const HISTORY_LIMIT = 24;

const CHART_VAR_NAMES = [
  '--primary',
  '--primary-intense',
  '--state-success',
  '--state-error',
  '--state-warning',
  '--text-secondary',
  '--text-tertiary',
  '--border-default',
  '--surface-elevated',
] as const;

type ChartVarName = (typeof CHART_VAR_NAMES)[number];

function useThemeColors(theme: string): Record<ChartVarName, string> {
  const [colors, setColors] = useState<Record<ChartVarName, string>>(
    () => Object.fromEntries(CHART_VAR_NAMES.map((n) => [n, '#888888'])) as Record<ChartVarName, string>
  );

  useEffect(() => {
    const computed = getComputedStyle(document.documentElement);
    const next = Object.fromEntries(
      CHART_VAR_NAMES.map((name) => [name, computed.getPropertyValue(name).trim() || '#888888'])
    ) as Record<ChartVarName, string>;
    setColors(next);
  }, [theme]);

  return colors;
}

function formatTime(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function Trend({ current, previous }: { current: number; previous: number | null }) {
  if (previous === null || previous === current) {
    return (
      <span className={`${styles.trend} ${styles.trendFlat}`}>
        <Minus size={12} /> No change
      </span>
    );
  }
  const up = current > previous;
  const diff = previous === 0 ? 100 : Math.abs(((current - previous) / previous) * 100);
  return (
    <span className={`${styles.trend} ${up ? styles.trendUp : styles.trendDown}`}>
      {up ? <ArrowUp size={12} /> : <ArrowDown size={12} />}
      {diff.toFixed(1)}%
    </span>
  );
}

export default function App() {
  const { theme, setTheme } = useTheme();
  const chartColors = useThemeColors(theme);

  const [stats, setStats] = useState<DashboardStatsDto | null>(null);
  const [isConnected, setIsConnected] = useState<boolean>(false);
  const [history, setHistory] = useState<HistoryPoint[]>([]);
  const prevStats = useRef<DashboardStatsDto | null>(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5025/hubs/stats')
      .withAutomaticReconnect()
      .build();

    connection
      .start()
      .then(() => setIsConnected(true))
      .catch((err: unknown) => console.error('SignalR Connection Error: ', err));

    connection.on('ReceiveStatsUpdate', (data: DashboardStatsDto) => {
      prevStats.current = stats;
      setStats(data);
      setHistory((prev) => {
        const next: HistoryPoint[] = [
          ...prev,
          {
            time: formatTime(data.timestamp),
            totalRequests: data.totalRequests,
            cacheHitRatio: data.cacheHitRatio,
            rateLimitBlocked: data.rateLimitBlocked,
          },
        ];
        return next.slice(-HISTORY_LIMIT);
      });
    });

    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));

    return () => {
      connection.stop();
    };
  }, []);

  const cachePieData = useMemo(
    () => [
      { name: 'Hits', value: stats?.cacheHits ?? 0 },
      { name: 'Misses', value: stats?.cacheMisses ?? 0 },
    ],
    [stats]
  );

  const serverBarData = useMemo(
    () =>
      (stats?.backendServers ?? []).map((s) => ({
        name: s.id,
        weight: s.weight,
        healthy: s.isHealthy,
      })),
    [stats]
  );

  const healthyCount = stats?.backendServers.filter((s) => s.isHealthy).length ?? 0;
  const totalServers = stats?.backendServers.length ?? 0;

  if (!stats) {
    return (
      <div className={styles.loadingScreen}>
        <div className={styles.loadingCard}>
          <Activity className={styles.spin} size={32} />
          <span>Connecting to Reverse Proxy Hub...</span>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.dashboard} dir="ltr">
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>
            <Activity className={styles.titleIcon} /> Reverse Proxy Monitoring Dashboard
          </h1>
          <p className={styles.subtitle}>
            <Radio size={13} className={isConnected ? styles.pulseIcon : undefined} /> Live updates via SignalR ·
            Last updated {formatTime(stats.timestamp)}
          </p>
        </div>
        <div className={styles.headerActions}>
          <div className={`${styles.connectionBadge} ${isConnected ? styles.connected : styles.disconnected}`}>
            <span className={styles.dot} />
            <span>{isConnected ? 'Connected to Proxy' : 'Disconnected'}</span>
          </div>
          <ThemeSwitcher theme={theme} onChange={setTheme} />
        </div>
      </header>

      <div className={styles.statGrid}>
        <div className={styles.statCard}>
          <div className={styles.statHead}>
            <span>Total Requests</span>
            <Activity size={18} className={styles.iconPrimary} />
          </div>
          <div className={styles.statValue}>{stats.totalRequests.toLocaleString('en-US')}</div>
          <Trend current={stats.totalRequests} previous={prevStats.current?.totalRequests ?? null} />
        </div>

        <div className={styles.statCard}>
          <div className={styles.statHead}>
            <span>Cache Hit Ratio</span>
            <Database size={18} className={styles.iconSuccess} />
          </div>
          <div className={`${styles.statValue} ${styles.textSuccess}`}>{stats.cacheHitRatio}%</div>
          <div className={styles.statMeta}>
            Hits: {stats.cacheHits.toLocaleString('en-US')} | Misses: {stats.cacheMisses.toLocaleString('en-US')}
          </div>
        </div>

        <div className={styles.statCard}>
          <div className={styles.statHead}>
            <span>Blocked Requests (Rate Limit)</span>
            <ShieldAlert size={18} className={styles.iconError} />
          </div>
          <div className={`${styles.statValue} ${styles.textError}`}>{stats.rateLimitBlocked}</div>
          <Trend current={stats.rateLimitBlocked} previous={prevStats.current?.rateLimitBlocked ?? null} />
        </div>

        <div className={styles.statCard}>
          <div className={styles.statHead}>
            <span>Active Servers</span>
            <Server size={18} className={styles.iconAccent} />
          </div>
          <div className={styles.statValue}>
            {healthyCount} / {totalServers}
          </div>
          <div className={styles.healthBar}>
            <div
              className={styles.healthBarFill}
              style={{ width: totalServers ? `${(healthyCount / totalServers) * 100}%` : '0%' }}
            />
          </div>
        </div>
      </div>

      <div className={styles.chartGrid}>
        <div className={`${styles.chartCard} ${styles.chartCardWide}`}>
          <h2 className={styles.cardTitle}>Requests and Cache Hit Ratio Over Time</h2>
          <ResponsiveContainer width="100%" height={230}>
            <AreaChart data={history} margin={{ top: 8, right: 8, left: -12, bottom: 0 }}>
              <defs>
                <linearGradient id="reqGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={chartColors['--primary']} stopOpacity={0.45} />
                  <stop offset="100%" stopColor={chartColors['--primary']} stopOpacity={0} />
                </linearGradient>
                <linearGradient id="ratioGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={chartColors['--state-success']} stopOpacity={0.35} />
                  <stop offset="100%" stopColor={chartColors['--state-success']} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke={chartColors['--border-default']} strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="time" stroke={chartColors['--text-tertiary']} fontSize={11} tickLine={false} />
              <YAxis yAxisId="left" stroke={chartColors['--text-tertiary']} fontSize={11} tickLine={false} axisLine={false} />
              <YAxis
                yAxisId="right"
                orientation="right"
                stroke={chartColors['--text-tertiary']}
                fontSize={11}
                tickLine={false}
                axisLine={false}
                unit="%"
              />
              <Tooltip
                contentStyle={{
                  background: chartColors['--surface-elevated'],
                  border: `1px solid ${chartColors['--border-default']}`,
                  borderRadius: 10,
                  fontSize: 12,
                }}
                labelStyle={{ color: chartColors['--text-secondary'] }}
              />
              <Area
                yAxisId="left"
                type="monotone"
                dataKey="totalRequests"
                name="Requests"
                stroke={chartColors['--primary']}
                fill="url(#reqGradient)"
                strokeWidth={2}
              />
              <Area
                yAxisId="right"
                type="monotone"
                dataKey="cacheHitRatio"
                name="Cache Ratio %"
                stroke={chartColors['--state-success']}
                fill="url(#ratioGradient)"
                strokeWidth={2}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        <div className={styles.chartCard}>
          <h2 className={styles.cardTitle}>Cache Distribution</h2>
          <ResponsiveContainer width="100%" height={230}>
            <PieChart>
              <Pie
                data={cachePieData}
                dataKey="value"
                nameKey="name"
                innerRadius={55}
                outerRadius={80}
                paddingAngle={3}
              >
                <Cell fill={chartColors['--state-success']} />
                <Cell fill={chartColors['--state-warning']} />
              </Pie>
              <Tooltip
                contentStyle={{
                  background: chartColors['--surface-elevated'],
                  border: `1px solid ${chartColors['--border-default']}`,
                  borderRadius: 10,
                  fontSize: 12,
                }}
              />
            </PieChart>
          </ResponsiveContainer>
          <div className={styles.legendRow}>
            <span>
              <i style={{ background: chartColors['--state-success'] }} /> Hits
            </span>
            <span>
              <i style={{ background: chartColors['--state-warning'] }} /> Misses
            </span>
          </div>
        </div>
      </div>

      <div className={styles.chartCard}>
        <h2 className={styles.cardTitle}>Backend Server Weights</h2>
        <ResponsiveContainer width="100%" height={200}>
          <BarChart data={serverBarData} margin={{ top: 8, right: 8, left: -12, bottom: 0 }}>
            <CartesianGrid stroke={chartColors['--border-default']} strokeDasharray="3 3" vertical={false} />
            <XAxis dataKey="name" stroke={chartColors['--text-tertiary']} fontSize={11} tickLine={false} />
            <YAxis stroke={chartColors['--text-tertiary']} fontSize={11} tickLine={false} axisLine={false} />
            <Tooltip
              contentStyle={{
                background: chartColors['--surface-elevated'],
                border: `1px solid ${chartColors['--border-default']}`,
                borderRadius: 10,
                fontSize: 12,
              }}
            />
            <Bar dataKey="weight" name="Weight" radius={[6, 6, 0, 0]}>
              {serverBarData.map((entry, index) => (
                <Cell
                  key={`bar-${index}`}
                  fill={entry.healthy ? chartColors['--primary'] : chartColors['--state-error']}
                />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className={styles.tableCard}>
        <h2 className={styles.cardTitle}>
          <Server size={20} className={styles.iconAccent} /> Backend Servers Status (Backend Nodes)
        </h2>
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Server ID</th>
                <th>URL</th>
                <th>Weight</th>
                <th>Health Status</th>
              </tr>
            </thead>
            <tbody>
              {stats.backendServers.map((server) => (
                <tr key={server.id}>
                  <td className={styles.mono}>{server.id}</td>
                  <td className={`${styles.mono} ${styles.textAccent}`}>{server.url}</td>
                  <td className={styles.bold}>{server.weight}</td>
                  <td>
                    {server.isHealthy ? (
                      <span className={`${styles.badge} ${styles.badgeSuccess}`}>
                        <CheckCircle2 size={14} /> Online
                      </span>
                    ) : (
                      <span className={`${styles.badge} ${styles.badgeError}`}>
                        <XCircle size={14} /> Offline
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}