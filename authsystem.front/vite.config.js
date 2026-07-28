import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import { federation } from '@module-federation/vite';
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export default defineConfig({
    plugins: [
        plugin(),
        federation({
            name: 'auth_module',
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
            key: fs.readFileSync(path.resolve(__dirname, "./.cert/main-container-key.pem")),
            cert: fs.readFileSync(path.resolve(__dirname, "./.cert/main-container-cert.pem")),
        }
    },
    // preview: {
    //     port: 60113,
    // },
    build: {
        target: 'esnext',
    }
});