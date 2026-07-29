import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import { federation } from '@module-federation/vite';
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

// Настройка путей для корректной работы в формате ES-модулей (ESM)
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export default defineConfig({
    plugins: [
        // Плагин для сборки и поддержки синтаксиса React (JSX, Fast Refresh)
        plugin(),

        // Конфигурация Module Federation для удаленного модуля (Remote)
        federation({
            // Уникальное имя микрофронтенда, по которому к нему будет обращаться Хост
            name: 'svod_reports_module',

            // Имя файла-манифеста, который будет генерироваться при сборке.
            // Хост загружает именно этот файл, чтобы узнать, какие компоненты доступны
            filename: 'remoteEntry.js',

            // Отключаем генерацию .d.ts типов TypeScript для экспортируемых файлов
            dts: false,

            // Список компонентов, которые данный микрофронтенд отдает "наружу" для Хоста
            exposes: {
                // Хост сможет импортировать этот компонент как: import('svod_reports/App')
                './App': './src/App.jsx',
            },

            // Описание общих зависимостей. Singleton гарантирует использование 
            // единого экземпляра React совместно с Хостом для избежания ошибок с хуками
            shared: {
                react: { singleton: true },
                'react-dom': { singleton: true }
            }
        }),
    ],

    // Настройки локального сервера для режима разработки (команда: npm run dev)
    server: {
        // Фиксированный порт, на котором запускается данный микрофронтенд
        port: 61572,

        // Включение HTTPS для локального сервера разработки. 
        // Это критически важно, так как Хост работает по HTTPS и браузер заблокирует 
        // загрузку remoteEntry.js по обычному HTTP из-за политик безопасности (Mixed Content)
        https: {
            // Абсолютные пути к SSL-сертификатам локальной машины
            key: fs.readFileSync(path.resolve(__dirname, "./.cert/key.pem")),
            cert: fs.readFileSync(path.resolve(__dirname, "./.cert/cert.pem")),
        },

        // Настройка прокси-сервера для обхода CORS ограничений при запросах к бэкенду
        proxy: {
            // Перенаправляет все запросы вида https://localhost:61572/api/* 
            // на локальный сервер бэкенда http://localhost:5008/api/*
            '/api': {
                target: 'https://localhost:7271',
                changeOrigin: true, // Меняет заголовок Origin на адрес целевого сервера
            },

            // Прокси для корректной подгрузки статических скриптов, картинок 
            // и стилей генератора отчетов FastReport с бэкенд-сервера
            '/_content': {
                target: 'https://localhost:7271',
                changeOrigin: true,
            }
        }
    },

    // Настройки сервера предпросмотра (команда: npm run preview)
    preview: {
        port: 61572
    },

    // Конфигурация финальной production-сборки
    build: {
        // esnext оставляет современный синтаксис (Top-level await, native ESM), 
        // необходимый для корректной работы Module Federation в браузере
        target: 'esnext',
    }
});
