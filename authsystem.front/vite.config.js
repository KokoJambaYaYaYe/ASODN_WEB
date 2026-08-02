import { defineConfig, loadEnv } from 'vite';
import plugin from '@vitejs/plugin-react';
import { federation } from '@module-federation/vite';
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export default defineConfig(({ mode }) => {

    const env = loadEnv(mode, process.cwd());

    return {
    // КРИТИЧЕСКИ ВАЖНО ДЛЯ PRODUCTION: указываем правильный базовый путь к папке микрофронтенда
        base: `${env.VITE_BASE_LOCATION}`,
        plugins: [
            plugin(),
            federation({
                name: 'authsystem_module',
                filename: 'remoteEntry.js',
                dts: false,
                // Указываем путь к нашему файлу с экспортами
                exposes: {
                    './components': './src/exports.js',
                },
                shared: {
                    react: { singleton: true },
                    'react-dom': { singleton: true }
                }
            }),
        ],
    server: {
        port: 60113,
            https: {
            // Использование path.resolve делает пути надежнее
            key: fs.readFileSync(path.resolve(__dirname, "./.cert/key.pem")),
                cert: fs.readFileSync(path.resolve(__dirname, "./.cert/cert.pem")),
        }
    },
    build: {
        target: 'esnext',
            },
};

});