#!/bin/bash

# SuperPanel Docker Startup Script

echo "🚀 Starting SuperPanel with Docker..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker first."
    exit 1
fi

# Build and start the services
echo "📦 Building and starting containers..."
docker-compose up --build -d

echo "⏳ Waiting for services to start..."
sleep 10

# Check service status
echo "🔍 Checking service status..."
docker-compose ps

echo ""
echo "✅ SuperPanel is starting up!"
echo ""
echo "🌐 Access points:"
echo "   • Web UI: http://localhost:3000"
echo "   • Web API: http://localhost:7001"
echo "   • Database: localhost:1433 (sa/SuperPanel123!)"
echo ""
echo "📋 Useful commands:"
echo "   • View logs: docker-compose logs -f"
echo "   • Stop services: docker-compose down"
echo "   • Restart: docker-compose restart"
echo "   • Rebuild: docker-compose up --build"
echo ""
echo "🔧 Troubleshooting:"
echo "   • If containers fail to start, check: docker-compose logs"
echo "   • To reset everything: docker-compose down -v && docker-compose up --build"